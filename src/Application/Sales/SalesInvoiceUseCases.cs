using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Numbering;
using Application.Costing;
using Application.Finance.AutoJournal;
using Application.Finance.Journals;
using Application.Inventory;
using Application.Production;
using Domain.Finance.JournalMappings;
using Domain.MasterData.Customers;
using Domain.MasterData.TaxCodes;
using Domain.Partnership.Cycles;
using Domain.Sales.DeliveryOrders;
using Domain.Sales.SalesInvoices;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Sales;

/// <summary>
/// Creates a draft invoice billing the delivery orders (same customer and branch). Returns the invoice id;
/// the number is given when the invoice is posted.
/// </summary>
public sealed record CreateSalesInvoiceCommand(
    IReadOnlyList<Guid> DeliveryOrderIds,
    DateOnly InvoiceDate,
    string? Notes) : ICommand<Guid>;

/// <summary>
/// Posts a draft invoice: gives it its number and journals the receivable. Returns the number.
/// </summary>
public sealed record PostSalesInvoiceCommand(Guid SalesInvoiceId) : ICommand<string>;

/// <summary>
/// Cancels a draft invoice; its delivery orders can be invoiced again.
/// </summary>
public sealed record CancelSalesInvoiceCommand(Guid SalesInvoiceId, string Reason) : ICommand;

internal sealed class CreateSalesInvoiceCommandValidator : AbstractValidator<CreateSalesInvoiceCommand>
{
    public CreateSalesInvoiceCommandValidator()
    {
        RuleFor(c => c.DeliveryOrderIds).NotEmpty();
        RuleForEach(c => c.DeliveryOrderIds).NotEmpty();
        RuleFor(c => c.Notes).MaximumLength(1000);
    }
}

internal sealed class CancelSalesInvoiceCommandValidator : AbstractValidator<CancelSalesInvoiceCommand>
{
    public CancelSalesInvoiceCommandValidator()
    {
        RuleFor(c => c.SalesInvoiceId).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(500);
    }
}

internal sealed class CreateSalesInvoiceCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<CreateSalesInvoiceCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateSalesInvoiceCommand command, CancellationToken cancellationToken)
    {
        Result notFuture = DailyRecordingSupport.EnsureNotFuture(command.InvoiceDate, dateTimeProvider);
        if (notFuture.IsFailure)
        {
            return Result.Failure<Guid>(notFuture.Error);
        }

        var ids = command.DeliveryOrderIds.Distinct().ToList();

        List<DeliveryOrder> deliveries = await context.DeliveryOrders
            .Include(d => d.Lines)
            .Where(d => ids.Contains(d.Id))
            .ToListAsync(cancellationToken);

        Guid missing = ids.Find(id => deliveries.TrueForAll(d => d.Id != id));
        if (missing != Guid.Empty)
        {
            return Result.Failure<Guid>(DeliveryOrderErrors.NotFound(missing));
        }

        foreach (Guid branchId in deliveries.Select(d => d.BranchId).Distinct())
        {
            Result access = await branchAccess.EnsureAccessAsync(branchId, cancellationToken);
            if (access.IsFailure)
            {
                return Result.Failure<Guid>(access.Error);
            }
        }

        // Keep the order the caller listed the deliveries in, for a predictable validation error.
        deliveries = [.. ids.Select(id => deliveries.Single(d => d.Id == id))];

        Customer? customer = await context.Customers.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == deliveries[0].CustomerId, cancellationToken);

        if (customer is null)
        {
            return Result.Failure<Guid>(CustomerErrors.NotFound(deliveries[0].CustomerId));
        }

        var taxCodeIds = deliveries
            .SelectMany(d => d.Lines)
            .Where(l => l.TaxCodeId is not null)
            .Select(l => l.TaxCodeId!.Value)
            .Distinct()
            .ToList();

        Dictionary<Guid, TaxCode> taxCodes = await context.TaxCodes.AsNoTracking()
            .Include(t => t.Rates)
            .Where(t => taxCodeIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        Result<SalesInvoice> invoice = SalesInvoice.CreateDraft(
            deliveries, command.InvoiceDate, customer.PaymentTermDays, command.Notes, taxCodes);

        if (invoice.IsFailure)
        {
            return Result.Failure<Guid>(invoice.Error);
        }

        context.SalesInvoices.Add(invoice.Value);

        await context.SaveChangesAsync(cancellationToken);

        return invoice.Value.Id;
    }
}

internal sealed class PostSalesInvoiceCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDocumentNumberGenerator numberGenerator,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<PostSalesInvoiceCommand, string>
{
    private const string DocumentPrefix = "INV";

    public async Task<Result<string>> Handle(PostSalesInvoiceCommand command, CancellationToken cancellationToken)
    {
        Result<SalesInvoice> invoice = await SalesSupport.LoadInvoiceAsync(context, branchAccess, command.SalesInvoiceId, cancellationToken);
        if (invoice.IsFailure)
        {
            return Result.Failure<string>(invoice.Error);
        }

        // Checked before taking a number so a rejected post does not consume one.
        Result postable = invoice.Value.EnsurePostable();
        if (postable.IsFailure)
        {
            return Result.Failure<string>(postable.Error);
        }

        // The journal is posted from the outbox; reject a closed period now rather than dead-letter it later.
        Result<Domain.Finance.FiscalPeriods.FiscalPeriod> period = await JournalSupport.FindPeriodAsync(
            context, invoice.Value.InvoiceDate, cancellationToken);

        if (period.IsFailure)
        {
            return Result.Failure<string>(period.Error);
        }

        // Estimated HPP: each cycle's running cost per kg at the moment of posting.
        var cycleIds = invoice.Value.Lines.Select(l => l.CycleId).Distinct().ToList();
        List<ProductionCycle> cycles = await context.ProductionCycles.AsNoTracking()
            .Where(c => cycleIds.Contains(c.Id))
            .ToListAsync(cancellationToken);

        var costPerKg = new Dictionary<Guid, decimal>();
        foreach (ProductionCycle cycle in cycles)
        {
            costPerKg[cycle.Id] = await CycleCosting.CostPerKgAsync(context, cycle, cancellationToken);
        }

        string number = await numberGenerator.NextForBranchAsync(
            context, DocumentPrefix, invoice.Value.BranchId, invoice.Value.InvoiceDate, cancellationToken);

        Result posted = invoice.Value.Post(number, userContext.UserId, dateTimeProvider.UtcNow, costPerKg);
        if (posted.IsFailure)
        {
            return Result.Failure<string>(posted.Error);
        }

        await context.SaveChangesAsync(cancellationToken);

        return number;
    }
}

internal sealed class CancelSalesInvoiceCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<CancelSalesInvoiceCommand>
{
    public async Task<Result> Handle(CancelSalesInvoiceCommand command, CancellationToken cancellationToken)
    {
        Result<SalesInvoice> invoice = await SalesSupport.LoadInvoiceAsync(context, branchAccess, command.SalesInvoiceId, cancellationToken);
        if (invoice.IsFailure)
        {
            return invoice;
        }

        List<DeliveryOrder> deliveries = await context.DeliveryOrders
            .Where(d => d.SalesInvoiceId == command.SalesInvoiceId)
            .ToListAsync(cancellationToken);

        Result result = invoice.Value.Cancel(command.Reason, deliveries);
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

/// <summary>
/// Journals a posted sales invoice: Dr piutang usaha / Cr penjualan ayam hidup (DPP), Dr piutang / Cr PPN keluaran,
/// and the estimated cost of the birds sold, Dr HPP / Cr ayam dalam proses (trued up when the cycle closes).
/// </summary>
internal sealed class SalesInvoicePostedDomainEventHandler(IApplicationDbContext context, IAutoJournalService autoJournal)
    : IDomainEventHandler<SalesInvoicePostedDomainEvent>
{
    public async Task Handle(SalesInvoicePostedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        SalesInvoice invoice = await context.SalesInvoices.AsNoTracking()
            .Include(i => i.Lines)
            .SingleAsync(i => i.Id == domainEvent.SalesInvoiceId, cancellationToken);

        string customer = await context.Customers
            .Where(c => c.Id == invoice.CustomerId)
            .Select(c => c.Name)
            .SingleAsync(cancellationToken);

        await InventoryAccounting.PostAsync(autoJournal, new AccountingEntry(
            AccountingEvents.SalesInvoice,
            invoice.Id,
            invoice.BranchId,
            invoice.InvoiceDate,
            $"Penjualan {invoice.Number} kepada {customer}",
            [
                new AccountingAmount("LiveBirdSales", invoice.Subtotal),
                new AccountingAmount("OutputVat", invoice.VatAmount),
                new AccountingAmount("CostOfGoodsSold", invoice.CostAmount)
            ]),
            cancellationToken);
    }
}
