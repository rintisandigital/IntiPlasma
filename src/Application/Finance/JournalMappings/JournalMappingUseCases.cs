using System.Data.Common;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Finance.AutoJournal;
using Application.Finance.Journals;
using Dapper;
using Domain.Finance.JournalMappings;
using Domain.Finance.Journals;
using Domain.MasterData.Branches;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.JournalMappings;

public sealed record JournalMappingLineRequest(string Component, Guid DebitAccountId, Guid CreditAccountId, Guid? CostCenterId);

/// <param name="BranchId">Null for the company-wide default mapping.</param>
public sealed record CreateJournalMappingCommand(
    string EventType,
    Guid? BranchId,
    string? Description,
    IReadOnlyList<JournalMappingLineRequest> Lines) : ICommand<Guid>;

public sealed record UpdateJournalMappingCommand(
    Guid JournalMappingId,
    string? Description,
    bool IsActive,
    IReadOnlyList<JournalMappingLineRequest> Lines) : ICommand;

public sealed record GetJournalMappingsQuery(string? EventType) : IQuery<IReadOnlyList<JournalMappingResponse>>;

public sealed record AccountingAmountRequest(
    string Component,
    decimal Amount,
    Guid? DebitAccountId,
    Guid? CreditAccountId,
    Guid? CostCenterId,
    string? Description);

/// <summary>
/// Dry run of the auto journal engine: shows the journal an accounting event would produce with the current mappings.
/// </summary>
public sealed record PreviewAutoJournalQuery(
    string EventType,
    Guid BranchId,
    DateOnly Date,
    IReadOnlyList<AccountingAmountRequest> Amounts) : IQuery<IReadOnlyList<PreviewJournalLine>>;

public sealed record PreviewJournalLine(Guid AccountId, string AccountCode, string AccountName, string? Description, decimal Debit, decimal Credit);

public sealed record JournalMappingResponse
{
    public Guid Id { get; init; }

    public string EventType { get; init; }

    public Guid? BranchId { get; init; }

    public string? BranchCode { get; init; }

    public string? Description { get; init; }

    public bool IsActive { get; init; }

    public IReadOnlyList<JournalMappingLineResponse> Lines { get; init; } = [];
}

public sealed record JournalMappingLineResponse(
    Guid JournalMappingId,
    string Component,
    Guid DebitAccountId,
    string DebitAccountCode,
    Guid CreditAccountId,
    string CreditAccountCode,
    Guid? CostCenterId);

internal sealed class JournalMappingLineRequestValidator : AbstractValidator<JournalMappingLineRequest>
{
    public JournalMappingLineRequestValidator()
    {
        RuleFor(l => l.Component).NotEmpty().MaximumLength(50);
        RuleFor(l => l.DebitAccountId).NotEmpty();
        RuleFor(l => l.CreditAccountId).NotEmpty();
    }
}

internal sealed class CreateJournalMappingCommandValidator : AbstractValidator<CreateJournalMappingCommand>
{
    public CreateJournalMappingCommandValidator()
    {
        RuleFor(c => c.EventType).NotEmpty().MaximumLength(50);
        RuleFor(c => c.Description).MaximumLength(500);
        RuleFor(c => c.Lines).NotEmpty();
        RuleForEach(c => c.Lines).SetValidator(new JournalMappingLineRequestValidator());
    }
}

internal sealed class UpdateJournalMappingCommandValidator : AbstractValidator<UpdateJournalMappingCommand>
{
    public UpdateJournalMappingCommandValidator()
    {
        RuleFor(c => c.JournalMappingId).NotEmpty();
        RuleFor(c => c.Description).MaximumLength(500);
        RuleFor(c => c.Lines).NotEmpty();
        RuleForEach(c => c.Lines).SetValidator(new JournalMappingLineRequestValidator());
    }
}

internal static class JournalMappingLines
{
    public static IReadOnlyList<(string Component, Guid DebitAccountId, Guid CreditAccountId, Guid? CostCenterId)> ToDomain(
        IReadOnlyList<JournalMappingLineRequest> lines) =>
        [.. lines.Select(l => (l.Component, l.DebitAccountId, l.CreditAccountId, l.CostCenterId))];

    public static Task<Result> ValidateReferencesAsync(
        IApplicationDbContext context,
        IReadOnlyList<JournalMappingLineRequest> lines,
        CancellationToken cancellationToken) =>
        JournalSupport.ValidateReferencesAsync(
            context,
            [
                .. lines.SelectMany(l => new[]
                {
                    new JournalLineInput(l.DebitAccountId, l.CostCenterId, null, Money.Zero, Money.Zero),
                    new JournalLineInput(l.CreditAccountId, null, null, Money.Zero, Money.Zero)
                })
            ],
            cancellationToken);
}

internal sealed class CreateJournalMappingCommandHandler(IApplicationDbContext context)
    : ICommandHandler<CreateJournalMappingCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateJournalMappingCommand command, CancellationToken cancellationToken)
    {
        if (command.BranchId is Guid branchId && !await context.Branches.AnyAsync(b => b.Id == branchId, cancellationToken))
        {
            return Result.Failure<Guid>(BranchErrors.NotFound(branchId));
        }

        if (await context.JournalMappings.AnyAsync(
                m => m.EventType == command.EventType && m.BranchId == command.BranchId, cancellationToken))
        {
            return Result.Failure<Guid>(JournalMappingErrors.AlreadyExists);
        }

        Result references = await JournalMappingLines.ValidateReferencesAsync(context, command.Lines, cancellationToken);
        if (references.IsFailure)
        {
            return Result.Failure<Guid>(references.Error);
        }

        Result<JournalMapping> mapping = JournalMapping.Create(
            command.EventType, command.BranchId, command.Description, JournalMappingLines.ToDomain(command.Lines));

        if (mapping.IsFailure)
        {
            return Result.Failure<Guid>(mapping.Error);
        }

        context.JournalMappings.Add(mapping.Value);

        await context.SaveChangesAsync(cancellationToken);

        return mapping.Value.Id;
    }
}

internal sealed class UpdateJournalMappingCommandHandler(IApplicationDbContext context)
    : ICommandHandler<UpdateJournalMappingCommand>
{
    public async Task<Result> Handle(UpdateJournalMappingCommand command, CancellationToken cancellationToken)
    {
        JournalMapping? mapping = await context.JournalMappings
            .Include(m => m.Lines)
            .SingleOrDefaultAsync(m => m.Id == command.JournalMappingId, cancellationToken);

        if (mapping is null)
        {
            return Result.Failure(JournalMappingErrors.NotFound(command.JournalMappingId));
        }

        Result references = await JournalMappingLines.ValidateReferencesAsync(context, command.Lines, cancellationToken);
        if (references.IsFailure)
        {
            return references;
        }

        Result result = mapping.Update(command.Description, command.IsActive, JournalMappingLines.ToDomain(command.Lines));
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class GetJournalMappingsQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetJournalMappingsQuery, IReadOnlyList<JournalMappingResponse>>
{
    public async Task<Result<IReadOnlyList<JournalMappingResponse>>> Handle(
        GetJournalMappingsQuery query,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string sql =
            """
            SELECT m.id AS Id, m.event_type AS EventType, m.branch_id AS BranchId, b.code AS BranchCode,
                   m.description AS Description, m.is_active AS IsActive
            FROM finance.journal_mappings m
            LEFT JOIN master.branches b ON b.id = m.branch_id
            WHERE (@EventType::text IS NULL OR m.event_type = @EventType)
            ORDER BY m.event_type, b.code NULLS FIRST;

            SELECT l.journal_mapping_id AS JournalMappingId, l.component AS Component,
                   l.debit_account_id AS DebitAccountId, d.code AS DebitAccountCode,
                   l.credit_account_id AS CreditAccountId, c.code AS CreditAccountCode, l.cost_center_id AS CostCenterId
            FROM finance.journal_mapping_lines l
            JOIN finance.accounts d ON d.id = l.debit_account_id
            JOIN finance.accounts c ON c.id = l.credit_account_id
            ORDER BY l.component;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { query.EventType }, cancellationToken: cancellationToken));

        List<JournalMappingResponse> mappings = [.. await multi.ReadAsync<JournalMappingResponse>()];
        ILookup<Guid, JournalMappingLineResponse> lines = (await multi.ReadAsync<JournalMappingLineResponse>())
            .ToLookup(l => l.JournalMappingId);

        return mappings.Select(m => m with { Lines = [.. lines[m.Id]] }).ToList();
    }
}

internal sealed class PreviewAutoJournalQueryHandler(IAutoJournalService autoJournal, IApplicationDbContext context)
    : IQueryHandler<PreviewAutoJournalQuery, IReadOnlyList<PreviewJournalLine>>
{
    public async Task<Result<IReadOnlyList<PreviewJournalLine>>> Handle(
        PreviewAutoJournalQuery query,
        CancellationToken cancellationToken)
    {
        var entry = new AccountingEntry(
            query.EventType,
            Guid.Empty,
            query.BranchId,
            query.Date,
            "Preview",
            [
                .. query.Amounts.Select(a => new AccountingAmount(
                    a.Component, new Money(a.Amount), a.DebitAccountId, a.CreditAccountId, a.CostCenterId, a.Description))
            ]);

        Result<IReadOnlyList<JournalLineInput>> lines = await autoJournal.BuildLinesAsync(entry, cancellationToken);
        if (lines.IsFailure)
        {
            return Result.Failure<IReadOnlyList<PreviewJournalLine>>(lines.Error);
        }

        var accountIds = lines.Value.Select(l => l.AccountId).Distinct().ToList();
        var accounts = await context.Accounts
            .Where(a => accountIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Code, a.Name })
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        return lines.Value
            .Select(l => new PreviewJournalLine(
                l.AccountId, accounts[l.AccountId].Code, accounts[l.AccountId].Name, l.Description, l.Debit.Amount, l.Credit.Amount))
            .ToList();
    }
}
