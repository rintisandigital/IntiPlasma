namespace Application.Abstractions.Numbering;

public interface IDocumentNumberGenerator
{
    /// <summary>
    /// Generates the next document number, e.g. <c>PO/BR01/2026/IX/0001</c> (month in Roman numerals).
    /// The sequence restarts every month per (prefix, branch). When called inside a transaction the
    /// number is only consumed if the transaction commits, so numbers stay gap-free.
    /// </summary>
    Task<string> NextAsync(string prefix, string branchCode, DateOnly documentDate, CancellationToken cancellationToken = default);
}
