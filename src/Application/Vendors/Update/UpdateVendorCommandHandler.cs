using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.MasterData.Vendors;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Vendors.Update;

internal sealed class UpdateVendorCommandHandler(IApplicationDbContext context)
    : ICommandHandler<UpdateVendorCommand>
{
    public async Task<Result> Handle(UpdateVendorCommand command, CancellationToken cancellationToken)
    {
        Vendor? vendor = await context.Vendors.SingleOrDefaultAsync(v => v.Id == command.VendorId, cancellationToken);

        if (vendor is null)
        {
            return Result.Failure(VendorErrors.NotFound(command.VendorId));
        }

        Result<TaxIdentity> taxIdentity = command.TaxIdentity.ToDomain();
        if (taxIdentity.IsFailure)
        {
            return taxIdentity;
        }

        Result<BankAccount> bankAccount = command.BankAccount.ToDomain();
        if (bankAccount.IsFailure)
        {
            return bankAccount;
        }

        vendor.Update(
            command.Name,
            taxIdentity.Value,
            command.Address,
            command.Phone,
            command.Email,
            command.PaymentTermDays,
            bankAccount.Value,
            command.IsActive);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
