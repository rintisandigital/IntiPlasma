using Domain.Common;
using SharedKernel;

namespace Domain.MasterData.Farmers;

/// <summary>
/// Peternak. An Inti farmer is a company-owned farm; a Plasma farmer is a partner who raises the
/// company's birds under a partnership contract (kontrak kemitraan). One farmer owns many coops.
/// </summary>
public sealed class Farmer : AggregateRoot, IHasDocuments
{
    private Farmer(Guid id, string code, FarmerType type, Guid branchId)
        : base(id)
    {
        Code = code;
        Type = type;
        BranchId = branchId;
        RegistrationStatus = RegistrationStatus.Draft;
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

    /// <summary>
    /// Only true once the farmer is approved (M-81).
    /// </summary>
    public bool IsActive { get; private set; }

    public RegistrationStatus RegistrationStatus { get; private set; }

    /// <summary>
    /// New NIK or bank account of an approved farmer waiting for approval (M-84); the current values stay in use.
    /// </summary>
    public FarmerPendingChange? PendingChange { get; private set; }

    /// <summary>
    /// PPL (petugas penyuluh lapangan) responsible for this farmer. Users limited to their assigned data
    /// (<c>partnership:assigned-only</c>) only see farmers (directly or through one of their coops) assigned to them.
    /// </summary>
    public Guid? FieldOfficerUserId { get; private set; }

    /// <summary>
    /// Lampiran: ids of the attached photos and documents.
    /// </summary>
    public Guid[] Documents { get; private set; } = [];

    public Result SetDocuments(IEnumerable<Guid>? documents) =>
        DocumentList.Apply(documents, value => Documents = value);

    public void AssignFieldOfficer(Guid? userId) =>
        FieldOfficerUserId = userId == Guid.Empty ? null : userId;

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

        Result result = farmer.Apply(name, nik, taxIdentity, address, phone, bankAccount, isActive: false);

        return result.IsSuccess ? farmer : Result.Failure<Farmer>(result.Error);
    }

    /// <summary>
    /// Changes the farmer. Not allowed while waiting for approval (M-83). For an approved farmer a different NIK or
    /// bank account is not applied but kept as <see cref="PendingChange"/> for a FarmerChange request (M-84); the value
    /// is then <c>true</c>. Only an approved farmer can be active.
    /// </summary>
    public Result<bool> Update(
        string name,
        string? nik,
        TaxIdentity taxIdentity,
        string? address,
        string? phone,
        BankAccount bankAccount,
        bool isActive)
    {
        Result editable = Registration.Edit(RegistrationStatus, nameof(Farmer), Id);
        if (editable.IsFailure)
        {
            return Result.Failure<bool>(editable.Error);
        }

        string? normalizedNik = Digits.Normalize(nik);
        bool proposeChange = RegistrationStatus == RegistrationStatus.Approved
            && (normalizedNik != Nik || bankAccount != BankAccount);

        if (proposeChange && PendingChange is not null)
        {
            return Result.Failure<bool>(FarmerErrors.ChangePending);
        }

        Result valid = Validate(normalizedNik);
        if (valid.IsFailure)
        {
            return Result.Failure<bool>(valid.Error);
        }

        Result result = proposeChange
            ? Apply(name, Nik, taxIdentity, address, phone, BankAccount, isActive)
            : Apply(name, nik, taxIdentity, address, phone, bankAccount, isActive);

        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error);
        }

        if (proposeChange)
        {
            PendingChange = FarmerPendingChange.Create(normalizedNik, bankAccount);
        }

        return proposeChange;
    }

    public Result Submit()
    {
        Result result = Registration.Submit(RegistrationStatus, nameof(Farmer), Id);
        if (result.IsSuccess)
        {
            RegistrationStatus = RegistrationStatus.PendingApproval;
        }

        return result;
    }

    public Result Approve()
    {
        Result result = Registration.Decide(RegistrationStatus, nameof(Farmer), Id);
        if (result.IsSuccess)
        {
            RegistrationStatus = RegistrationStatus.Approved;
            IsActive = true;
        }

        return result;
    }

    public Result Reject()
    {
        Result result = Registration.Decide(RegistrationStatus, nameof(Farmer), Id);
        if (result.IsSuccess)
        {
            RegistrationStatus = RegistrationStatus.Rejected;
        }

        return result;
    }

    /// <summary>
    /// The requester cancelled the request: the farmer can be edited and submitted again.
    /// </summary>
    public Result ReturnToDraft()
    {
        Result result = Registration.Decide(RegistrationStatus, nameof(Farmer), Id);
        if (result.IsSuccess)
        {
            RegistrationStatus = RegistrationStatus.Draft;
        }

        return result;
    }

    public void ApplyPendingChange()
    {
        if (PendingChange is null)
        {
            return;
        }

        Nik = PendingChange.Nik;
        BankAccount = PendingChange.ToBankAccount();
        PendingChange = null;
    }

    public void DiscardPendingChange() => PendingChange = null;

    private Result Apply(
        string name,
        string? nik,
        TaxIdentity taxIdentity,
        string? address,
        string? phone,
        BankAccount bankAccount,
        bool isActive)
    {
        string? normalizedNik = Digits.Normalize(nik);

        Result valid = Validate(normalizedNik);
        if (valid.IsFailure)
        {
            return valid;
        }

        Name = name.Trim();
        Nik = normalizedNik;
        TaxIdentity = taxIdentity;
        Address = address;
        Phone = phone;
        BankAccount = bankAccount;
        IsActive = isActive && RegistrationStatus == RegistrationStatus.Approved;

        return Result.Success();
    }

    private Result Validate(string? normalizedNik)
    {
        if (normalizedNik is not null && !Digits.IsDigits(normalizedNik, 16))
        {
            return Result.Failure(CommonErrors.InvalidNik);
        }

        return Type == FarmerType.Plasma && normalizedNik is null
            ? Result.Failure(FarmerErrors.PlasmaRequiresNik)
            : Result.Success();
    }
}
