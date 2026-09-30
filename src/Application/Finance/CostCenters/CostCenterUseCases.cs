using System.Data.Common;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using Domain.Finance.CostCenters;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.CostCenters;

public sealed record CreateCostCenterCommand(string Code, string Name) : ICommand<Guid>;

public sealed record UpdateCostCenterCommand(Guid CostCenterId, string Name, bool IsActive) : ICommand;

public sealed record GetCostCentersQuery : IQuery<IReadOnlyList<CostCenterResponse>>;

public sealed record CostCenterResponse(Guid Id, string Code, string Name, bool IsActive);

internal sealed class CreateCostCenterCommandValidator : AbstractValidator<CreateCostCenterCommand>
{
    public CreateCostCenterCommandValidator()
    {
        RuleFor(c => c.Code).NotEmpty().MaximumLength(20);
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
    }
}

internal sealed class UpdateCostCenterCommandValidator : AbstractValidator<UpdateCostCenterCommand>
{
    public UpdateCostCenterCommandValidator()
    {
        RuleFor(c => c.CostCenterId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
    }
}

internal sealed class CreateCostCenterCommandHandler(IApplicationDbContext context)
    : ICommandHandler<CreateCostCenterCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateCostCenterCommand command, CancellationToken cancellationToken)
    {
        var costCenter = CostCenter.Create(command.Code, command.Name);

        if (await context.CostCenters.AnyAsync(c => c.Code == costCenter.Code, cancellationToken))
        {
            return Result.Failure<Guid>(CostCenterErrors.CodeNotUnique(costCenter.Code));
        }

        context.CostCenters.Add(costCenter);

        await context.SaveChangesAsync(cancellationToken);

        return costCenter.Id;
    }
}

internal sealed class UpdateCostCenterCommandHandler(IApplicationDbContext context)
    : ICommandHandler<UpdateCostCenterCommand>
{
    public async Task<Result> Handle(UpdateCostCenterCommand command, CancellationToken cancellationToken)
    {
        CostCenter? costCenter = await context.CostCenters
            .SingleOrDefaultAsync(c => c.Id == command.CostCenterId, cancellationToken);

        if (costCenter is null)
        {
            return Result.Failure(CostCenterErrors.NotFound(command.CostCenterId));
        }

        costCenter.Update(command.Name, command.IsActive);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class GetCostCentersQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetCostCentersQuery, IReadOnlyList<CostCenterResponse>>
{
    public async Task<Result<IReadOnlyList<CostCenterResponse>>> Handle(GetCostCentersQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        IEnumerable<CostCenterResponse> costCenters = await connection.QueryAsync<CostCenterResponse>(new CommandDefinition(
            "SELECT c.id AS Id, c.code AS Code, c.name AS Name, c.is_active AS IsActive FROM finance.cost_centers c ORDER BY c.code",
            cancellationToken: cancellationToken));

        return costCenters.ToList();
    }
}
