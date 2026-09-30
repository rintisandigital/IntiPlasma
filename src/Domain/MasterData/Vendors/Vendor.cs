using Domain.Common;
using SharedKernel;

namespace Domain.MasterData.Vendors;

/// <summary>
/// Supplier of sapronak (DOC hatchery, feed mill, OVK distributor) or services.
/// </summary>
public sealed class Vendor : AggregateRoot
{
    private Vendor(Guid id, string code, string name)
        : base(id)
    {
        Code = code;
        Name = name;
        IsActive = true;
    }

    private Vendor()
    {
    }

    public string Code { get; private set; }
    public string Name { get; private set; }
    public TaxIdentity TaxIdentity { get; private set; } = TaxIdentity.None;
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public int PaymentTermDays { get; private set; }
    public BankAccount BankAccount { get; private set; } = BankAccount.None;
    public bool IsActive { get; private set; }

    public static Vendor Create(
        string code,
        string name,
        TaxIdentity taxIdentity,
        string? address,
        string? phone,
        string? email,
        int paymentTermDays,
        BankAccount bankAccount)
    {
        var vendor = new Vendor(Guid.CreateVersion7(), Codes.Normalize(code), name.Trim());

        vendor.Update(name, taxIdentity, address, phone, email, paymentTermDays, bankAccount, isActive: true);

        return vendor;
    }

    public void Update(
        string name,
        TaxIdentity taxIdentity,
        string? address,
        string? phone,
        string? email,
        int paymentTermDays,
        BankAccount bankAccount,
        bool isActive)
    {
        Name = name.Trim();
        TaxIdentity = taxIdentity;
        Address = address;
        Phone = phone;
        Email = email;
        PaymentTermDays = paymentTermDays;
        BankAccount = bankAccount;
        IsActive = isActive;
    }
}
