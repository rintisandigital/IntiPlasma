namespace Web.App.Models.Shared;

/// <summary>
/// Model of <c>_ListHeader</c>: title, export buttons (Export right) and the add button (Create right).
/// </summary>
public sealed record ListHeaderModel(string Title, string Subtitle, string MenuCode, string? AddText = null);

/// <summary>
/// Model of <c>_FormButtons</c>: Save (only when the user may save) and Cancel.
/// </summary>
public sealed record FormButtonsModel(bool CanSave, string CancelUrl, string SaveText = "Save");
