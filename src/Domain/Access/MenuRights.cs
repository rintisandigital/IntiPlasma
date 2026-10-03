namespace Domain.Access;

/// <summary>
/// Rights on one Web.App menu (W-2). Stored as separate boolean columns; combined as flags in code.
/// </summary>
[Flags]
public enum MenuRights
{
    None = 0,
    View = 1,
    Create = 2,
    Edit = 4,
    Delete = 8,
    Export = 16
}
