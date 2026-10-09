namespace Domain.Roles;

/// <summary>
/// Names of the default API roles for the mobile app (PLAN-MOBILE §4.2). They are seeded once and remain ordinary
/// roles that administrators may edit; the mobile app decides its menus from permissions, never from these names.
/// </summary>
public static class MobileRoles
{
    /// <summary>
    /// Petugas Penyuluh Lapangan (field officer).
    /// </summary>
    public const string FieldOfficer = "PPL";

    public const string Manager = "Manager";
}
