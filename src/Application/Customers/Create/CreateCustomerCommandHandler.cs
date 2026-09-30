using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.MasterData.Customers;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Customers.Create;

internal sealed class CreateCustomerCommandHandler(IApplicationDbContext context)
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

        context.Customers.Add(customer);

        await context.SaveChangesAsync(cancellationToken);

        return customer.Id;
    }
}
