namespace Infrastructure.Numbering;

internal sealed class DocumentSequence
{
    /// <summary>
    /// "{prefix}/{branch}/{yyyy}/{roman month}", e.g. "PO/BR01/2026/IX".
    /// </summary>
    public string Key { get; init; }

    public long LastValue { get; init; }
}
