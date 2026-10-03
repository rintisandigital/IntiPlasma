using Application.Abstractions.Auditing;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Auditing;
using SharedKernel;

namespace Application.Auditing;

/// <summary>
/// Records an export (Excel, PDF, CSV) or a printed document in the audit trail (category Export).
/// </summary>
/// <param name="Menu">Menu code of the exported screen, e.g. <c>sales.orders</c>.</param>
/// <param name="Title">File/report title.</param>
/// <param name="Format">Excel, Pdf or Csv.</param>
/// <param name="Rows">Exported rows (0 for a printed document).</param>
/// <param name="Filters">Screen filters the export used, as shown in the file header.</param>
/// <param name="Branch">Active branch of the export (null = all branches).</param>
/// <param name="DocumentId">The printed document, if any.</param>
public sealed record RecordExportCommand(
    string Menu,
    string Title,
    string Format,
    int Rows,
    IReadOnlyList<string> Filters,
    string? Branch = null,
    Guid? DocumentId = null) : ICommand;

internal sealed class RecordExportCommandHandler(IApplicationDbContext context, IAuditTrail auditTrail)
    : ICommandHandler<RecordExportCommand>
{
    public async Task<Result> Handle(RecordExportCommand command, CancellationToken cancellationToken)
    {
        string summary = command.DocumentId is null
            ? $"{command.Format} export of {command.Title} ({command.Rows} rows)"
            : $"{command.Format} print of {command.Title}";

        auditTrail.Record(new AuditEntry(AuditCategory.Export, command.Format, summary)
        {
            EntityType = command.Menu,
            EntityId = command.DocumentId,
            Details = new { command.Menu, command.Title, command.Rows, command.Branch, command.Filters }
        });

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
