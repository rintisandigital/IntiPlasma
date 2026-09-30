using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.MasterData.Branches;
using Domain.MasterData.Farmers;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Farmers.Create;

internal sealed class CreateFarmerCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<CreateFarmerCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateFarmerCommand command, CancellationToken cancellationToken)
    {
        Result access = await branchAccess.EnsureAccessAsync(command.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<Guid>(access.Error);
        }

        if (!await context.Branches.AnyAsync(b => b.Id == command.BranchId && b.IsActive, cancellationToken))
        {
            return Result.Failure<Guid>(BranchErrors.NotFound(command.BranchId));
        }

        Result<TaxIdentity> taxIdentity = command.TaxIdentity.ToDomain();
        if (taxIdentity.IsFailure)
        {
            return Result.Failure<Guid>(taxIdentity.Error);
        }

        Result<BankAccount> bankAccount = command.BankAccount.ToDomain();
        if (bankAccount.IsFailure)
        {
            return Result.Failure<Guid>(bankAccount.Error);
        }

        Result<Farmer> farmer = Farmer.Create(
            command.Code,
            command.Name,
            command.Type,
            command.BranchId,
            command.Nik,
            taxIdentity.Value,
            command.Address,
            command.Phone,
            bankAccount.Value);

        if (farmer.IsFailure)
        {
            return Result.Failure<Guid>(farmer.Error);
        }

        if (await context.Farmers.AnyAsync(f => f.Code == farmer.Value.Code, cancellationToken))
        {
            return Result.Failure<Guid>(FarmerErrors.CodeNotUnique(farmer.Value.Code));
        }

        context.Farmers.Add(farmer.Value);

        await context.SaveChangesAsync(cancellationToken);

        return farmer.Value.Id;
    }
}
