using System.Data.Common;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using Domain.Finance.Accounts;
using Domain.Finance.Journals;
using Domain.Finance.JournalTemplates;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.JournalTemplates;

public sealed record JournalTemplateLineRequest(Guid AccountId, Guid? CostCenterId, BalanceSide Side, string? Description);

public sealed record CreateJournalTemplateCommand(
    string Name,
    string? Description,
    IReadOnlyList<JournalTemplateLineRequest> Lines) : ICommand<Guid>;

public sealed record UpdateJournalTemplateCommand(
    Guid JournalTemplateId,
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyList<JournalTemplateLineRequest> Lines) : ICommand;

public sealed record GetJournalTemplatesQuery : IQuery<IReadOnlyList<JournalTemplateResponse>>;

public sealed record JournalTemplateResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; }

    public string? Description { get; init; }

    public bool IsActive { get; init; }

    public IReadOnlyList<JournalTemplateLineResponse> Lines { get; init; } = [];
}

public sealed record JournalTemplateLineResponse(
    Guid JournalTemplateId,
    int LineNumber,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    Guid? CostCenterId,
    string Side,
    string? Description);

internal sealed class JournalTemplateLineRequestValidator : AbstractValidator<JournalTemplateLineRequest>
{
    public JournalTemplateLineRequestValidator()
    {
        RuleFor(l => l.AccountId).NotEmpty();
        RuleFor(l => l.Side).IsInEnum();
        RuleFor(l => l.Description).MaximumLength(250);
    }
}

internal sealed class CreateJournalTemplateCommandValidator : AbstractValidator<CreateJournalTemplateCommand>
{
    public CreateJournalTemplateCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Description).MaximumLength(500);
        RuleFor(c => c.Lines).NotNull();
        RuleForEach(c => c.Lines).SetValidator(new JournalTemplateLineRequestValidator());
    }
}

internal sealed class UpdateJournalTemplateCommandValidator : AbstractValidator<UpdateJournalTemplateCommand>
{
    public UpdateJournalTemplateCommandValidator()
    {
        RuleFor(c => c.JournalTemplateId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Description).MaximumLength(500);
        RuleFor(c => c.Lines).NotNull();
        RuleForEach(c => c.Lines).SetValidator(new JournalTemplateLineRequestValidator());
    }
}

internal static class JournalTemplateLines
{
    public static IReadOnlyList<(Guid AccountId, Guid? CostCenterId, BalanceSide Side, string? Description)> ToDomain(
        IReadOnlyList<JournalTemplateLineRequest> lines) =>
        [.. lines.Select(l => (l.AccountId, l.CostCenterId, l.Side, l.Description))];

    /// <summary>
    /// Reuses the journal reference rules by checking the template lines as zero-value journal lines.
    /// </summary>
    public static Task<Result> ValidateReferencesAsync(
        IApplicationDbContext context,
        IReadOnlyList<JournalTemplateLineRequest> lines,
        CancellationToken cancellationToken) =>
        Journals.JournalSupport.ValidateReferencesAsync(
            context,
            [.. lines.Select(l => new JournalLineInput(l.AccountId, l.CostCenterId, null, Money.Zero, Money.Zero))],
            cancellationToken);
}

internal sealed class CreateJournalTemplateCommandHandler(IApplicationDbContext context)
    : ICommandHandler<CreateJournalTemplateCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateJournalTemplateCommand command, CancellationToken cancellationToken)
    {
        if (await context.JournalTemplates.AnyAsync(t => t.Name == command.Name.Trim(), cancellationToken))
        {
            return Result.Failure<Guid>(JournalTemplateErrors.NameNotUnique);
        }

        Result references = await JournalTemplateLines.ValidateReferencesAsync(context, command.Lines, cancellationToken);
        if (references.IsFailure)
        {
            return Result.Failure<Guid>(references.Error);
        }

        Result<JournalTemplate> template = JournalTemplate.Create(
            command.Name, command.Description, JournalTemplateLines.ToDomain(command.Lines));

        if (template.IsFailure)
        {
            return Result.Failure<Guid>(template.Error);
        }

        context.JournalTemplates.Add(template.Value);

        await context.SaveChangesAsync(cancellationToken);

        return template.Value.Id;
    }
}

internal sealed class UpdateJournalTemplateCommandHandler(IApplicationDbContext context)
    : ICommandHandler<UpdateJournalTemplateCommand>
{
    public async Task<Result> Handle(UpdateJournalTemplateCommand command, CancellationToken cancellationToken)
    {
        JournalTemplate? template = await context.JournalTemplates
            .Include(t => t.Lines)
            .SingleOrDefaultAsync(t => t.Id == command.JournalTemplateId, cancellationToken);

        if (template is null)
        {
            return Result.Failure(JournalTemplateErrors.NotFound(command.JournalTemplateId));
        }

        if (await context.JournalTemplates.AnyAsync(
                t => t.Id != command.JournalTemplateId && t.Name == command.Name.Trim(), cancellationToken))
        {
            return Result.Failure(JournalTemplateErrors.NameNotUnique);
        }

        Result references = await JournalTemplateLines.ValidateReferencesAsync(context, command.Lines, cancellationToken);
        if (references.IsFailure)
        {
            return references;
        }

        Result result = template.Update(
            command.Name, command.Description, command.IsActive, JournalTemplateLines.ToDomain(command.Lines));

        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class GetJournalTemplatesQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetJournalTemplatesQuery, IReadOnlyList<JournalTemplateResponse>>
{
    public async Task<Result<IReadOnlyList<JournalTemplateResponse>>> Handle(
        GetJournalTemplatesQuery query,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string sql =
            """
            SELECT t.id AS Id, t.name AS Name, t.description AS Description, t.is_active AS IsActive
            FROM finance.journal_templates t
            ORDER BY t.name;

            SELECT l.journal_template_id AS JournalTemplateId, l.line_number AS LineNumber, l.account_id AS AccountId,
                   a.code AS AccountCode, a.name AS AccountName, l.cost_center_id AS CostCenterId, l.side AS Side,
                   l.description AS Description
            FROM finance.journal_template_lines l
            JOIN finance.accounts a ON a.id = l.account_id
            ORDER BY l.line_number;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        List<JournalTemplateResponse> templates = [.. await multi.ReadAsync<JournalTemplateResponse>()];
        ILookup<Guid, JournalTemplateLineResponse> lines = (await multi.ReadAsync<JournalTemplateLineResponse>())
            .ToLookup(l => l.JournalTemplateId);

        return templates.Select(t => t with { Lines = [.. lines[t.Id]] }).ToList();
    }
}
