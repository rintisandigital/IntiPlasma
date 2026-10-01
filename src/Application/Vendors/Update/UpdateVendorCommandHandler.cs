using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Documents;
using Domain.Common;
using Domain.Documents.Attachments;
using Domain.MasterData.Vendors;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Vendors.Update;

internal sealed class UpdateVendorCommandHandler(IApplicationDbContext context, IAttachmentService attachments)
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

        // Omitted tolerance keeps the current one.
        if (command.PriceTolerancePercent is not null)
        {
            Result tolerance = vendor.SetPriceTolerance(command.PriceTolerancePercent.Value);
            if (tolerance.IsFailure)
            {
                return tolerance;
            }
        }

        Result documents = await attachments.ApplyDocumentsAsync(
            vendor,
            AttachmentOwner.Of(AttachmentOwnerTypes.Vendor, vendor.Id),
            null,
            command.Documents,
            cancellationToken);
        if (documents.IsFailure)
        {
            return documents;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
