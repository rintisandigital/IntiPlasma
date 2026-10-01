using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Documents;
using Domain.Common;
using Domain.Documents.Attachments;
using Domain.MasterData.Customers;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Customers.Create;

internal sealed class CreateCustomerCommandHandler(IApplicationDbContext context, IAttachmentService attachments)
    : ICommandHandler<CreateCustomerCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        Result<TaxIdentity> taxIdentity = command.TaxIdentity.ToDomain();
        if (taxIdentity.IsFailure)
        {
            return Result.Failure<Guid>(taxIdentity.Error);
        }

        var customer = Customer.Create(
            command.Code,
            command.Name,
            taxIdentity.Value,
            command.Address,
            command.Phone,
            command.Email,
            command.PaymentTermDays,
            new Money(command.CreditLimit));

        if (await context.Customers.AnyAsync(c => c.Code == customer.Code, cancellationToken))
        {
            return Result.Failure<Guid>(CustomerErrors.CodeNotUnique(customer.Code));
        }

        Result documents = await attachments.ApplyDocumentsAsync(
            customer,
            AttachmentOwner.Of(AttachmentOwnerTypes.Customer, customer.Id),
            null,
            command.Documents,
            cancellationToken);
        if (documents.IsFailure)
        {
            return Result.Failure<Guid>(documents.Error);
        }

        context.Customers.Add(customer);

        await context.SaveChangesAsync(cancellationToken);

        return customer.Id;
    }
}
