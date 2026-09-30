using System.Data.Common;

namespace Application.Abstractions.Data;

/// <summary>
/// Read side of CQRS: query handlers open a connection and run SQL (Dapper) that projects directly into responses.
/// </summary>
public interface IDbConnectionFactory
{
    ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
