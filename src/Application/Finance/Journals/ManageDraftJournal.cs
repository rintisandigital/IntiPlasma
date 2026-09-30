using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Finance.Journals;
using Domain.MasterData.Branches;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.Journals;

public sealed record CreateJournalCommand(
    Guid BranchId,
    DateOnly Date,
    string Description,
    IReadOnlyList<JournalLineRequest> Lines) : ICommand<Guid>;

public sealed record UpdateJournalCommand(
    Guid JournalId,
    DateOnly Date,
    string Description,
    IReadOnlyList<JournalLineRequest> Lines) : ICommand;

public sealed record DeleteJournalCommand(Guid JournalId) : ICommand;

internal sealed class CreateJournalCommandValidator : AbstractValidator<CreateJournalCommand>
{
    public CreateJournalCommandValidator()
    {
        RuleFor(c => c.BranchId).NotEmpty();
        RuleFor(c => c.Description).NotEmpty().MaximumLength(500);
        RuleFor(c => c.Lines).NotNull();
        RuleForEach(c => c.Lines).SetValidator(new JournalLineRequestValidator());
    }
}

internal sealed class UpdateJournalCommandValidator : AbstractValidator<UpdateJournalCommand>
{
    public UpdateJournalCommandValidator()
    {
        RuleFor(c => c.JournalId).NotEmpty();
        RuleFor(c => c.Description).NotEmpty().MaximumLength(500);
        RuleFor(c => c.Lines).NotNull();
        RuleForEach(c => c.Lines).SetValidator(new JournalLineRequestValidator());
    }
}

internal sealed class CreateJournalCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<CreateJournalCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateJournalCommand command, CancellationToken cancellationToken)
    {
        Result access = await branchAccess.EnsureAccessAsync(command.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<Guid>(access.Error);
        }

        if (!await context.Branches.AnyAsync(b => b.Id == command.BranchId, cancellationToken))
        {
            return Result.Failure<Guid>(BranchErrors.NotFound(command.BranchId));
        }

        var lines = command.Lines.Select(l => l.ToDomain()).ToList();

        Result references = await JournalSupport.ValidateReferencesAsync(context, lines, cancellationToken);
        if (references.IsFailure)
        {
            return Result.Failure<Guid>(references.Error);
        }

        Result<JournalEntry> journal = JournalEntry.CreateManual(command.BranchId, command.Date, command.Description, lines);
        if (journal.IsFailure)
        {
            return Result.Failure<Guid>(journal.Error);
        }

        context.JournalEntries.Add(journal.Value);

        await context.SaveChangesAsync(cancellationToken);

        return journal.Value.Id;
    }
}

internal sealed class UpdateJournalCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<UpdateJournalCommand>
{
    public async Task<Result> Handle(UpdateJournalCommand command, CancellationToken cancellationToken)
    {
        Result<JournalEntry> journal = await JournalLoader.LoadAsync(context, branchAccess, command.JournalId, cancellationToken);
        if (journal.IsFailure)
        {
            return journal;
        }

        var lines = command.Lines.Select(l => l.ToDomain()).ToList();

        Result references = await JournalSupport.ValidateReferencesAsync(context, lines, cancellationToken);
        if (references.IsFailure)
        {
            return references;
        }

        Result result = journal.Value.Update(command.Date, command.Description, lines);
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class DeleteJournalCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<DeleteJournalCommand>
{
    public async Task<Result> Handle(DeleteJournalCommand command, CancellationToken cancellationToken)
    {
        Result<JournalEntry> journal = await JournalLoader.LoadAsync(context, branchAccess, command.JournalId, cancellationToken);
        if (journal.IsFailure)
        {
            return journal;
        }

        if (!journal.Value.CanBeDeleted)
        {
            return Result.Failure(JournalErrors.NotEditable(command.JournalId));
        }

        context.JournalEntries.Remove(journal.Value);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal static class JournalLoader
{
    public static async Task<Result<JournalEntry>> LoadAsync(
        IApplicationDbContext context,
        IBranchAccess branchAccess,
        Guid journalId,
        CancellationToken cancellationToken)
    {
        JournalEntry? journal = await context.JournalEntries
            .Include(j => j.Lines)
            .SingleOrDefaultAsync(j => j.Id == journalId, cancellationToken);

        if (journal is null)
        {
            return Result.Failure<JournalEntry>(JournalErrors.NotFound(journalId));
        }

        Result access = await branchAccess.EnsureAccessAsync(journal.BranchId, cancellationToken);

        return access.IsSuccess ? journal : Result.Failure<JournalEntry>(access.Error);
    }
}
