using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.MasterData.Vendors;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Vendors.Create;

internal sealed class CreateVendorCommandHandler(IApplicationDbContext context)
    : ICommandHandler<CreateVendorCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateVendorCommand command, CancellationToken cancellationToken)
    {
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

        var vendor = Vendor.Create(
            command.Code,
            command.Name,
            taxIdentity.Value,
            command.Address,
            command.Phone,
            command.Email,
            command.PaymentTermDays,
            bankAccount.Value);

        if (await context.Vendors.AnyAsync(v => v.Code == vendor.Code, cancellationToken))
        {
            return Result.Failure<Guid>(VendorErrors.CodeNotUnique(vendor.Code));
        }

        context.Vendors.Add(vendor);

        await context.SaveChangesAsync(cancellationToken);

        return vendor.Id;
    }
}
