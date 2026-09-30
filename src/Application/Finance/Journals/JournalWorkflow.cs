using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Numbering;
using Domain.Finance.FiscalPeriods;
using Domain.Finance.Journals;
using FluentValidation;
using SharedKernel;

namespace Application.Finance.Journals;

public sealed record ApproveJournalCommand(Guid JournalId) : ICommand;

public sealed record PostJournalCommand(Guid JournalId) : ICommand<string>;

public sealed record ReverseJournalCommand(Guid JournalId, DateOnly Date, string Reason) : ICommand<Guid>;

internal sealed class ReverseJournalCommandValidator : AbstractValidator<ReverseJournalCommand>
{
    public ReverseJournalCommandValidator()
    {
        RuleFor(c => c.JournalId).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(300);
    }
}

internal sealed class ApproveJournalCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<ApproveJournalCommand>
{
    public async Task<Result> Handle(ApproveJournalCommand command, CancellationToken cancellationToken)
    {
        Result<JournalEntry> journal = await JournalLoader.LoadAsync(context, branchAccess, command.JournalId, cancellationToken);
        if (journal.IsFailure)
        {
            return journal;
        }

        Result result = journal.Value.Approve(userContext.UserId, dateTimeProvider.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class PostJournalCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider,
    IDocumentNumberGenerator numberGenerator) : ICommandHandler<PostJournalCommand, string>
{
    public async Task<Result<string>> Handle(PostJournalCommand command, CancellationToken cancellationToken)
    {
        Result<JournalEntry> journal = await JournalLoader.LoadAsync(context, branchAccess, command.JournalId, cancellationToken);
        if (journal.IsFailure)
        {
            return Result.Failure<string>(journal.Error);
        }

        if (journal.Value.Status != JournalStatus.Approved)
        {
            return Result.Failure<string>(JournalErrors.InvalidTransition(journal.Value.Status, JournalStatus.Posted));
        }

        Result<FiscalPeriod> period = await JournalSupport.FindPeriodAsync(context, journal.Value.Date, cancellationToken);
        if (period.IsFailure)
        {
            return Result.Failure<string>(period.Error);
        }

        string number = await JournalSupport.NextNumberAsync(
            context, numberGenerator, journal.Value.Source, journal.Value.BranchId, journal.Value.Date, cancellationToken);

        Result result = journal.Value.Post(number, period.Value, userContext.UserId, dateTimeProvider.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<string>(result.Error);
        }

        await context.SaveChangesAsync(cancellationToken);

        return number;
    }
}

internal sealed class ReverseJournalCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider,
    IDocumentNumberGenerator numberGenerator) : ICommandHandler<ReverseJournalCommand, Guid>
{
    public async Task<Result<Guid>> Handle(ReverseJournalCommand command, CancellationToken cancellationToken)
    {
        Result<JournalEntry> journal = await JournalLoader.LoadAsync(context, branchAccess, command.JournalId, cancellationToken);
        if (journal.IsFailure)
        {
            return Result.Failure<Guid>(journal.Error);
        }

        if (journal.Value.Status != JournalStatus.Posted)
        {
            return Result.Failure<Guid>(JournalErrors.InvalidTransition(journal.Value.Status, JournalStatus.Reversed));
        }

        Result<FiscalPeriod> period = await JournalSupport.FindPeriodAsync(context, command.Date, cancellationToken);
        if (period.IsFailure)
        {
            return Result.Failure<Guid>(period.Error);
        }

        string number = await JournalSupport.NextNumberAsync(
            context, numberGenerator, JournalSource.Manual, journal.Value.BranchId, command.Date, cancellationToken);

        Result<JournalEntry> reversal = journal.Value.Reverse(
            command.Date, command.Reason, number, period.Value, userContext.UserId, dateTimeProvider.UtcNow);

        if (reversal.IsFailure)
        {
            return Result.Failure<Guid>(reversal.Error);
        }

        context.JournalEntries.Add(reversal.Value);

        await context.SaveChangesAsync(cancellationToken);

        return reversal.Value.Id;
    }
}
