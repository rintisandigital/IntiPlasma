using Domain.Common;
using SharedKernel;

namespace Domain.MasterData.Farmers;

/// <summary>
/// Peternak. An Inti farmer is a company-owned farm; a Plasma farmer is a partner who raises the
/// company's birds under a partnership contract (kontrak kemitraan). One farmer owns many coops.
/// </summary>
public sealed class Farmer : AggregateRoot
{
    private Farmer(Guid id, string code, FarmerType type, Guid branchId)
        : base(id)
    {
        Code = code;
        Type = type;
        BranchId = branchId;
        IsActive = true;
    }

    private Farmer()
    {
    }

    public string Code { get; private set; }
    public string Name { get; private set; }
    public FarmerType Type { get; private set; }
    public Guid BranchId { get; private set; }

    /// <summary>
    /// Nomor Induk Kependudukan, required for plasma farmers (individual partners).
    /// </summary>
    public string? Nik { get; private set; }

    public TaxIdentity TaxIdentity { get; private set; } = TaxIdentity.None;
    public string? Address { get; private set; }
    public string? Phone { get; private set; }

    /// <summary>
    /// Destination of plasma settlement payments.
    /// </summary>
    public BankAccount BankAccount { get; private set; } = BankAccount.None;

    public bool IsActive { get; private set; }

    public static Result<Farmer> Create(
        string code,
        string name,
        FarmerType type,
        Guid branchId,
        string? nik,
        TaxIdentity taxIdentity,
        string? address,
        string? phone,
        BankAccount bankAccount)
    {
        var farmer = new Farmer(Guid.CreateVersion7(), Codes.Normalize(code), type, branchId);

        Result result = farmer.Update(name, nik, taxIdentity, address, phone, bankAccount, isActive: true);

        return result.IsSuccess ? farmer : Result.Failure<Farmer>(result.Error);
    }

    public Result Update(
        string name,
        string? nik,
        TaxIdentity taxIdentity,
        string? address,
        string? phone,
        BankAccount bankAccount,
        bool isActive)
    {
        string? normalizedNik = Digits.Normalize(nik);

        if (normalizedNik is not null && !Digits.IsDigits(normalizedNik, 16))
        {
            return Result.Failure(CommonErrors.InvalidNik);
        }

        if (Type == FarmerType.Plasma && normalizedNik is null)
        {
            return Result.Failure(FarmerErrors.PlasmaRequiresNik);
        }

        Name = name.Trim();
        Nik = normalizedNik;
        TaxIdentity = taxIdentity;
        Address = address;
        Phone = phone;
        BankAccount = bankAccount;
        IsActive = isActive;

        return Result.Success();
    }
}
