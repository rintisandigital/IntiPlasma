using SharedKernel;

namespace Domain.Finance.Payables;

/// <summary>
/// A posted document the company owes and pays with a payment voucher: a vendor invoice (payee = vendor) or a plasma
/// settlement (payee = farmer).
/// </summary>
public interface IPayable
{
    Guid Id { get; }

    Guid BranchId { get; }

    /// <summary>
    /// The vendor or farmer the amount is owed to.
    /// </summary>
    Guid PayeeId { get; }

    string? Number { get; }

    DateOnly DocumentDate { get; }

    bool IsPayable { get; }

    Money Outstanding { get; }

    Result RegisterPayment(Money amount);
}
