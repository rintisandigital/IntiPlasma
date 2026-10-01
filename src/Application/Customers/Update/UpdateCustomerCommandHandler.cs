using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Documents;
using Domain.Common;
using Domain.Documents.Attachments;
using Domain.MasterData.Customers;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Customers.Update;

internal sealed class UpdateCustomerCommandHandler(IApplicationDbContext context, IAttachmentService attachments)
    : ICommandHandler<UpdateCustomerCommand>
{
    public async Task<Result> Handle(UpdateCustomerCommand command, CancellationToken cancellationToken)
    {
        Customer? customer = await context.Customers
            .SingleOrDefaultAsync(c => c.Id == command.CustomerId, cancellationToken);

        if (customer is null)
        {
            return Result.Failure(CustomerErrors.NotFound(command.CustomerId));
        }

        Result<TaxIdentity> taxIdentity = command.TaxIdentity.ToDomain();
        if (taxIdentity.IsFailure)
        {
            return taxIdentity;
        }

        customer.Update(
            command.Name,
            taxIdentity.Value,
            command.Address,
            command.Phone,
            command.Email,
            command.PaymentTermDays,
            new Money(command.CreditLimit),
            command.IsActive);

        Result documents = await attachments.ApplyDocumentsAsync(
            customer,
            AttachmentOwner.Of(AttachmentOwnerTypes.Customer, customer.Id),
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
