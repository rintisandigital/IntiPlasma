using Domain.Roles;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Application.Abstractions.Data;

/// <summary>
/// Write side of CQRS: command handlers load aggregates through these sets, call domain methods and save.
/// Read side query handlers should prefer <see cref="IDbConnectionFactory"/> (Dapper + SQL).
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Role> Roles { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
