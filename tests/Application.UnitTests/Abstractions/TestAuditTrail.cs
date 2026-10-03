using Application.Abstractions.Auditing;
using Domain.Auditing;

namespace Application.UnitTests.Abstractions;

/// <summary>
/// Writes audit entries to the test database like the real implementation (without HTTP context).
/// </summary>
internal sealed class TestAuditTrail(TestDbContext context) : IAuditTrail
{
    public void Record(AuditEntry entry) =>
        context.AuditLogs.Add(AuditLog.Create(
            DateTime.UtcNow,
            entry.Category,
            entry.Action,
            entry.Summary,
            "Tests",
            entry.UserId,
            entry.UserEmail,
            entry.EntityType,
            entry.EntityId,
            entry.Details?.ToString()));
}
