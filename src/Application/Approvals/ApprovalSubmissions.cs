using System.Globalization;
using Domain.Approvals;
using Domain.Common;
using Domain.MasterData.Coops;
using Domain.MasterData.Farmers;
using Domain.Partnership.Contracts;

namespace Application.Approvals;

/// <summary>
/// What the inbox shows of each document type (code, title, one-line summary and changed values).
/// </summary>
internal static class ApprovalSubmissions
{
    public static ApprovalSubmission ForFarmer(Farmer farmer) =>
        new(
            ApprovalDocumentType.Farmer,
            farmer.Id,
            farmer.BranchId,
            farmer.Code,
            farmer.Name,
            Join(farmer.Type.ToString(), farmer.Nik is null ? null : $"NIK {farmer.Nik}", farmer.Phone));

    /// <summary>
    /// The proposed NIK and bank account next to the values in use (M-84).
    /// </summary>
    public static ApprovalSubmission ForFarmerChange(Farmer farmer)
    {
        FarmerPendingChange change = farmer.PendingChange!;
        List<ApprovalChange> changes = [];

        if (change.Nik != farmer.Nik)
        {
            changes.Add(new ApprovalChange("Nik", farmer.Nik, change.Nik));
        }

        string? oldAccount = Describe(farmer.BankAccount);
        string? newAccount = Describe(change.ToBankAccount());
        if (oldAccount != newAccount)
        {
            changes.Add(new ApprovalChange("BankAccount", oldAccount, newAccount));
        }

        return new(
            ApprovalDocumentType.FarmerChange,
            farmer.Id,
            farmer.BranchId,
            farmer.Code,
            farmer.Name,
            string.Join(", ", changes.Select(c => c.Field)),
            changes);
    }

    public static ApprovalSubmission ForCoop(Coop coop, Farmer farmer) =>
        new(
            ApprovalDocumentType.Coop,
            coop.Id,
            coop.BranchId,
            coop.Code,
            coop.Name,
            Join($"{farmer.Code} {farmer.Name}", coop.Capacity.ToString("N0", CultureInfo.InvariantCulture), coop.HouseType.ToString()));

    public static ApprovalSubmission ForContract(PartnershipContract contract) =>
        new(
            ApprovalDocumentType.Contract,
            contract.Id,
            contract.BranchId,
            contract.Code,
            contract.Name,
            Join(
                contract.Scheme.ToString(),
                contract.ValidTo is { } validTo
                    ? $"{contract.ValidFrom:yyyy-MM-dd} – {validTo:yyyy-MM-dd}"
                    : $"{contract.ValidFrom:yyyy-MM-dd} –"));

    private static string? Describe(BankAccount account) =>
        account.AccountNumber is null ? null : $"{account.BankName} {account.AccountNumber} a.n. {account.AccountHolderName}";

    private static string Join(params string?[] parts) =>
        string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
}
