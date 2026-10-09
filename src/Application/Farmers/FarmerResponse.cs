using Application.Common;

namespace Application.Farmers;

public sealed record FarmerResponse
{
    /// <summary>
    /// Lampiran: attachment ids; metadata via <c>GET /attachments?ids=</c>.
    /// </summary>
    public Guid[] Documents { get; init; } = [];

    public Guid Id { get; init; }

    public string Code { get; init; }

    public string Name { get; init; }

    public string Type { get; init; }

    public Guid BranchId { get; init; }

    public string BranchCode { get; init; }

    public string? Nik { get; init; }

    public TaxIdentityResponse TaxIdentity { get; init; }

    public string? Address { get; init; }

    public string? Phone { get; init; }

    public BankAccountResponse BankAccount { get; init; }

    public int CoopCount { get; init; }

    public bool IsActive { get; init; }

    /// <summary>
    /// PPL responsible for the farmer (PLAN-MOBILE M-11).
    /// </summary>
    public Guid? FieldOfficerUserId { get; init; }

    public string? FieldOfficerName { get; init; }
}
