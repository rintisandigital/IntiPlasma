using Domain.Common;
using SharedKernel;

namespace Domain.MasterData.Customers;

public static class CustomerErrors
{
    public static Error NotFound(Guid customerId) => Error.NotFound(
        "Customers.NotFound",
        $"The customer with the Id = '{customerId}' was not found");

    public static Error CodeNotUnique(string code) => CommonErrors.CodeNotUnique("Customer", code);
}
