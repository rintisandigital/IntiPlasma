using Domain.Common;
using SharedKernel;

namespace Domain.MasterData.Customers;

/// <summary>
/// Buyer of live birds (bakul, RPA/rumah potong ayam, trader).
/// </summary>
public sealed class Customer : AggregateRoot, IHasDocuments
{
    private Customer(Guid id, string code, string name)
        : base(id)
    {
        Code = code;
        Name = name;
        IsActive = true;
    }

    private Customer()
    {
    }

    public string Code { get; private set; }
    public string Name { get; private set; }
    public TaxIdentity TaxIdentity { get; private set; } = TaxIdentity.None;
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public int PaymentTermDays { get; private set; }

    /// <summary>
    /// Maximum outstanding receivable; zero means no credit (cash before delivery).
    /// </summary>
    public Money CreditLimit { get; private set; } = Money.Zero;

    public bool IsActive { get; private set; }

    /// <summary>
    /// Lampiran: ids of the attached photos and documents.
    /// </summary>
    public Guid[] Documents { get; private set; } = [];

    public Result SetDocuments(IEnumerable<Guid>? documents) =>
        DocumentList.Apply(documents, value => Documents = value);

    public static Customer Create(
        string code,
        string name,
        TaxIdentity taxIdentity,
        string? address,
        string? phone,
        string? email,
        int paymentTermDays,
        Money creditLimit)
    {
        var customer = new Customer(Guid.CreateVersion7(), Codes.Normalize(code), name.Trim());

        customer.Update(name, taxIdentity, address, phone, email, paymentTermDays, creditLimit, isActive: true);

        return customer;
    }

    public void Update(
        string name,
        TaxIdentity taxIdentity,
        string? address,
        string? phone,
        string? email,
        int paymentTermDays,
        Money creditLimit,
        bool isActive)
    {
        Name = name.Trim();
        TaxIdentity = taxIdentity;
        Address = address;
        Phone = phone;
        Email = email;
        PaymentTermDays = paymentTermDays;
        CreditLimit = creditLimit;
        IsActive = isActive;
    }
}
