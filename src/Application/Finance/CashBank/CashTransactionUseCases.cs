using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Numbering;
using Application.Documents;
using Application.Finance.AutoJournal;
using Application.Finance.Journals;
using Application.Production;
using Domain.Documents.Attachments;
using Domain.Finance.CashBank;
using Domain.Finance.JournalMappings;
using Domain.Finance.Journals;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.CashBank;

/// <param name="AccountId">Counter account: credited for cash-in (e.g. pendapatan bunga), debited for cash-out (e.g. beban listrik).</param>
public sealed record CashTransactionLineRequest(Guid AccountId, Guid? CostCenterId, string? Description, decimal Amount);

/// <summary>
/// Records a cash-in or cash-out draft. Cash-in can be posted right away; cash-out must be approved by someone else.
/// </summary>
public sealed record CreateCashTransactionCommand(
    Guid CashBankAccountId,
    CashDirection Direction,
    DateOnly Date,
    string Description,
    string? Reference,
    IReadOnlyList<CashTransactionLineRequest> Lines,
    IReadOnlyList<Guid>? Documents = null) : ICommand<Guid>;

public sealed record ApproveCashTransactionCommand(Guid CashTransactionId) : ICommand;

/// <summary>
/// Posts the transaction: gives it its number (BKM/BKK) and journals it. Returns the number.
/// </summary>
public sealed record PostCashTransactionCommand(Guid CashTransactionId) : ICommand<string>;

public sealed record CancelCashTransactionCommand(Guid CashTransactionId, string Reason) : ICommand;

/// <summary>
/// Moves money between two cash/bank accounts of a branch (e.g. petty cash replenishment). Posted immediately.
/// </summary>
public sealed record CreateBankTransferCommand(
    Guid FromCashBankAccountId,
    Guid ToCashBankAccountId,
    DateOnly Date,
    decimal Amount,
    string? Reference,
    string? Notes) : ICommand<CreateBankTransferResponse>;

public sealed record CreateBankTransferResponse(Guid Id, string Number);

internal sealed class CreateCashTransactionCommandValidator : AbstractValidator<CreateCashTransactionCommand>
{
    public CreateCashTransactionCommandValidator()
    {
        RuleFor(c => c.CashBankAccountId).NotEmpty();
        RuleFor(c => c.Direction).IsInEnum();
        RuleFor(c => c.Description).NotEmpty().MaximumLength(500);
        RuleFor(c => c.Reference).MaximumLength(100);
        RuleFor(c => c.Lines).NotEmpty();
        RuleForEach(c => c.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.AccountId).NotEmpty();
            line.RuleFor(l => l.Amount).GreaterThan(0);
            line.RuleFor(l => l.Description).MaximumLength(250);
        });
        RuleFor(c => c.Documents).ValidDocuments();
    }
}

internal sealed class CancelCashTransactionCommandValidator : AbstractValidator<CancelCashTransactionCommand>
{
    public CancelCashTransactionCommandValidator()
    {
        RuleFor(c => c.CashTransactionId).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(500);
    }
}

internal sealed class CreateBankTransferCommandValidator : AbstractValidator<CreateBankTransferCommand>
{
    public CreateBankTransferCommandValidator()
    {
        RuleFor(c => c.FromCashBankAccountId).NotEmpty();
        RuleFor(c => c.ToCashBankAccountId).NotEmpty();
        RuleFor(c => c.Amount).GreaterThan(0);
        RuleFor(c => c.Reference).MaximumLength(100);
        RuleFor(c => c.Notes).MaximumLength(1000);
    }
}

internal static class CashTransactionSupport
{
    public static async Task<Result<CashTransaction>> LoadAsync(
        IApplicationDbContext context,
        IBranchAccess branchAccess,
        Guid cashTransactionId,
        CancellationToken cancellationToken)
    {
        CashTransaction? transaction = await context.CashTransactions
            .Include(t => t.Lines)
            .SingleOrDefaultAsync(t => t.Id == cashTransactionId, cancellationToken);

        if (transaction is null)
        {
            return Result.Failure<CashTransaction>(CashTransactionErrors.NotFound(cashTransactionId));
        }

        Result access = await branchAccess.EnsureAccessAsync(transaction.BranchId, cancellationToken);

        return access.IsSuccess ? transaction : Result.Failure<CashTransaction>(access.Error);
    }
}

internal sealed class CreateCashTransactionCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDateTimeProvider dateTimeProvider,
    IAttachmentService attachments) : ICommandHandler<CreateCashTransactionCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateCashTransactionCommand command, CancellationToken cancellationToken)
    {
        Result notFuture = DailyRecordingSupport.EnsureNotFuture(command.Date, dateTimeProvider);
        if (notFuture.IsFailure)
        {
            return Result.Failure<Guid>(notFuture.Error);
        }

        CashBankAccount? cashBank = await context.CashBankAccounts.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == command.CashBankAccountId, cancellationToken);

        if (cashBank is null)
        {
            return Result.Failure<Guid>(CashBankErrors.NotFound(command.CashBankAccountId));
        }

        Result access = await branchAccess.EnsureAccessAsync(cashBank.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<Guid>(access.Error);
        }

        if (!cashBank.IsActive)
        {
            return Result.Failure<Guid>(CashBankErrors.Unusable(cashBank.Id));
        }

        List<CashTransactionLineInput> lines =
        [
            .. command.Lines.Select(l => new CashTransactionLineInput(l.AccountId, l.CostCenterId, l.Description, new Money(l.Amount)))
        ];

        // The counter accounts must be postable and active, like any journal line.
        Result references = await JournalSupport.ValidateReferencesAsync(
            context,
            [.. lines.Select(l => JournalLineInput.DebitLine(l.AccountId, l.Amount, l.CostCenterId))],
            cancellationToken);

        if (references.IsFailure)
        {
            return Result.Failure<Guid>(references.Error);
        }

        Result<CashTransaction> transaction = CashTransaction.Create(
            cashBank.BranchId, cashBank.Id, cashBank.AccountId, command.Direction, command.Date, command.Description,
            command.Reference, lines);

        if (transaction.IsFailure)
        {
            return Result.Failure<Guid>(transaction.Error);
        }

        Result documents = await attachments.ApplyDocumentsAsync(
            transaction.Value,
            AttachmentOwner.Of(AttachmentOwnerTypes.CashTransaction, transaction.Value.Id),
            transaction.Value.BranchId,
            command.Documents,
            cancellationToken);
        if (documents.IsFailure)
        {
            return Result.Failure<Guid>(documents.Error);
        }

        context.CashTransactions.Add(transaction.Value);

        await context.SaveChangesAsync(cancellationToken);

        return transaction.Value.Id;
    }
}

internal sealed class ApproveCashTransactionCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<ApproveCashTransactionCommand>
{
    public async Task<Result> Handle(ApproveCashTransactionCommand command, CancellationToken cancellationToken)
    {
        Result<CashTransaction> transaction = await CashTransactionSupport.LoadAsync(
            context, branchAccess, command.CashTransactionId, cancellationToken);

        if (transaction.IsFailure)
        {
            return transaction;
        }

        Result result = transaction.Value.Approve(userContext.UserId, dateTimeProvider.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class PostCashTransactionCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDocumentNumberGenerator numberGenerator,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<PostCashTransactionCommand, string>
{
    public async Task<Result<string>> Handle(PostCashTransactionCommand command, CancellationToken cancellationToken)
    {
        Result<CashTransaction> transaction = await CashTransactionSupport.LoadAsync(
            context, branchAccess, command.CashTransactionId, cancellationToken);

        if (transaction.IsFailure)
        {
            return Result.Failure<string>(transaction.Error);
        }

        // Checked before taking a number so a rejected post does not consume one.
        Result postable = transaction.Value.EnsurePostable();
        if (postable.IsFailure)
        {
            return Result.Failure<string>(postable.Error);
        }

        // The journal is posted from the outbox; reject now rather than dead-letter it later.
        Result<Domain.Finance.FiscalPeriods.FiscalPeriod> period = await JournalSupport.FindPeriodAsync(
            context, transaction.Value.Date, cancellationToken);

        if (period.IsFailure)
        {
            return Result.Failure<string>(period.Error);
        }

        string prefix = transaction.Value.Direction == CashDirection.In ? "BKM" : "BKK";
        string number = await numberGenerator.NextForBranchAsync(
            context, prefix, transaction.Value.BranchId, transaction.Value.Date, cancellationToken);

        Result posted = transaction.Value.Post(number, userContext.UserId, dateTimeProvider.UtcNow);
        if (posted.IsFailure)
        {
            return Result.Failure<string>(posted.Error);
        }

        await context.SaveChangesAsync(cancellationToken);

        return number;
    }
}

internal sealed class CancelCashTransactionCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<CancelCashTransactionCommand>
{
    public async Task<Result> Handle(CancelCashTransactionCommand command, CancellationToken cancellationToken)
    {
        Result<CashTransaction> transaction = await CashTransactionSupport.LoadAsync(
            context, branchAccess, command.CashTransactionId, cancellationToken);

        if (transaction.IsFailure)
        {
            return transaction;
        }

        Result result = transaction.Value.Cancel(command.Reason);
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class CreateBankTransferCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDocumentNumberGenerator numberGenerator,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<CreateBankTransferCommand, CreateBankTransferResponse>
{
    private const string DocumentPrefix = "TRF";

    public async Task<Result<CreateBankTransferResponse>> Handle(CreateBankTransferCommand command, CancellationToken cancellationToken)
    {
        Result notFuture = DailyRecordingSupport.EnsureNotFuture(command.Date, dateTimeProvider);
        if (notFuture.IsFailure)
        {
            return Result.Failure<CreateBankTransferResponse>(notFuture.Error);
        }

        List<CashBankAccount> accounts = await context.CashBankAccounts.AsNoTracking()
            .Where(a => a.Id == command.FromCashBankAccountId || a.Id == command.ToCashBankAccountId)
            .ToListAsync(cancellationToken);

        CashBankAccount? from = accounts.Find(a => a.Id == command.FromCashBankAccountId);
        CashBankAccount? to = accounts.Find(a => a.Id == command.ToCashBankAccountId);

        if (from is null || to is null)
        {
            return Result.Failure<CreateBankTransferResponse>(
                CashBankErrors.NotFound(from is null ? command.FromCashBankAccountId : command.ToCashBankAccountId));
        }

        Result access = await branchAccess.EnsureAccessAsync(from.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<CreateBankTransferResponse>(access.Error);
        }

        var amount = new Money(command.Amount);

        // Validated with a placeholder number first so a rejected transfer does not consume a document number.
        Result<BankTransfer> validation = BankTransfer.Create(string.Empty, from, to, command.Date, amount, command.Reference, command.Notes);
        if (validation.IsFailure)
        {
            return Result.Failure<CreateBankTransferResponse>(validation.Error);
        }

        string number = await numberGenerator.NextForBranchAsync(context, DocumentPrefix, from.BranchId, command.Date, cancellationToken);

        BankTransfer transfer = BankTransfer.Create(number, from, to, command.Date, amount, command.Reference, command.Notes).Value;

        context.BankTransfers.Add(transfer);

        await context.SaveChangesAsync(cancellationToken);

        return new CreateBankTransferResponse(transfer.Id, transfer.Number);
    }
}

/// <summary>
/// Journals a cash-in (Dr kas/bank / Cr each line's account) or cash-out (Dr each line's account / Cr kas/bank).
/// </summary>
internal sealed class CashTransactionPostedDomainEventHandler(IApplicationDbContext context, IAutoJournalService autoJournal)
    : IDomainEventHandler<CashTransactionPostedDomainEvent>
{
    public async Task Handle(CashTransactionPostedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        CashTransaction transaction = await context.CashTransactions.AsNoTracking()
            .Include(t => t.Lines)
            .SingleAsync(t => t.Id == domainEvent.CashTransactionId, cancellationToken);

        Guid cashAccountId = await context.CashBankAccounts
            .Where(a => a.Id == transaction.CashBankAccountId)
            .Select(a => a.AccountId)
            .SingleAsync(cancellationToken);

        bool cashIn = transaction.Direction == CashDirection.In;

        List<JournalLineInput> lines =
        [
            .. transaction.Lines.Select(l => cashIn
                ? JournalLineInput.CreditLine(l.AccountId, l.Amount, l.CostCenterId, l.Description)
                : JournalLineInput.DebitLine(l.AccountId, l.Amount, l.CostCenterId, l.Description))
        ];

        lines.Add(cashIn
            ? JournalLineInput.DebitLine(cashAccountId, transaction.Amount)
            : JournalLineInput.CreditLine(cashAccountId, transaction.Amount));

        await AutoJournalPosting.EnsureAsync(
            autoJournal.PostLinesAsync(
                AccountingEvents.CashTransaction,
                transaction.Id,
                transaction.BranchId,
                transaction.Date,
                $"{transaction.Number}: {transaction.Description}",
                lines,
                cancellationToken),
            $"cash transaction {transaction.Number}");
    }
}

/// <summary>
/// Journals a transfer between cash/bank accounts: Dr destination / Cr source.
/// </summary>
internal sealed class BankTransferPostedDomainEventHandler(IApplicationDbContext context, IAutoJournalService autoJournal)
    : IDomainEventHandler<BankTransferPostedDomainEvent>
{
    public async Task Handle(BankTransferPostedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        BankTransfer transfer = await context.BankTransfers.AsNoTracking()
            .SingleAsync(t => t.Id == domainEvent.BankTransferId, cancellationToken);

        Dictionary<Guid, (Guid AccountId, string Name)> accounts = await context.CashBankAccounts
            .Where(a => a.Id == transfer.FromCashBankAccountId || a.Id == transfer.ToCashBankAccountId)
            .ToDictionaryAsync(a => a.Id, a => (a.AccountId, a.Name), cancellationToken);

        (Guid fromAccount, string fromName) = accounts[transfer.FromCashBankAccountId];
        (Guid toAccount, string toName) = accounts[transfer.ToCashBankAccountId];

        await AutoJournalPosting.EnsureAsync(
            autoJournal.PostLinesAsync(
                AccountingEvents.BankTransfer,
                transfer.Id,
                transfer.BranchId,
                transfer.Date,
                $"Pemindahbukuan {transfer.Number}: {fromName} ke {toName}",
                [JournalLineInput.DebitLine(toAccount, transfer.Amount), JournalLineInput.CreditLine(fromAccount, transfer.Amount)],
                cancellationToken),
            $"bank transfer {transfer.Number}");
    }
}
