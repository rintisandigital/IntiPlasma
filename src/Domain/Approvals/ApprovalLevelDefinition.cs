namespace Domain.Approvals;

/// <summary>
/// One level of an approval flow: a free-text name (e.g. "Verifikasi", "Kepala Cabang") and the users of whom any
/// one decides the level (M-29, M-96).
/// </summary>
public sealed record ApprovalLevelDefinition(string Name, IReadOnlyList<Guid> ApproverUserIds);
