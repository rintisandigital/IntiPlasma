using Application.Abstractions.Auditing;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Domain.Auditing;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users;

/// <summary>
/// Lockout after too many wrong passwords (W10), bound from <c>Security:Lockout</c>.
/// </summary>
public sealed class LockoutOptions
{
    public const string SectionName = "Security:Lockout";

    public int MaxFailedAttempts { get; set; } = 5;

    public int LockoutMinutes { get; set; } = 15;
}

/// <summary>
/// Password check shared by the Web.App sign-in and the Web.Api login: wrong passwords are counted, the account
/// is locked out after <see cref="LockoutOptions.MaxFailedAttempts"/>, and every attempt is recorded in the audit
/// trail (category SignIn).
/// </summary>
internal sealed class CredentialVerifier(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    IDateTimeProvider dateTimeProvider,
    LockoutOptions lockout,
    IAuditTrail auditTrail)
{
    public const string SignedIn = "SignedIn";
    public const string SignInFailed = "SignInFailed";
    public const string LockedOut = "LockedOut";
    public const string SignInRejected = "SignInRejected";

    /// <summary>
    /// Returns the (tracked) user when the password is right and the account is not locked out. The caller checks
    /// the account status, then calls <see cref="SucceedAsync"/> or <see cref="RejectAsync"/>.
    /// </summary>
    public async Task<Result<User>> VerifyAsync(string email, string password, CancellationToken cancellationToken)
    {
        DateTime now = dateTimeProvider.UtcNow;

        User? user = await context.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
        {
            Record(SignInFailed, "Sign-in failed: unknown email", null, email);
            await SaveAsync(cancellationToken);

            return Result.Failure<User>(UserErrors.InvalidCredentials);
        }

        if (user.IsLockedOut(now))
        {
            Record(SignInRejected, "Sign-in refused: account locked out", user);
            await SaveAsync(cancellationToken);

            return Result.Failure<User>(UserErrors.LockedOut(RemainingMinutes(user, now)));
        }

        if (!passwordHasher.Verify(password, user.PasswordHash))
        {
            bool lockedOut = user.RegisterFailedSignIn(
                now, lockout.MaxFailedAttempts, TimeSpan.FromMinutes(lockout.LockoutMinutes));

            if (lockedOut)
            {
                Record(LockedOut, $"Account locked out after {lockout.MaxFailedAttempts} failed sign-ins", user);
            }
            else
            {
                Record(SignInFailed, "Sign-in failed: wrong password", user);
            }

            await SaveAsync(cancellationToken);

            return Result.Failure<User>(lockedOut
                ? UserErrors.LockedOut(RemainingMinutes(user, now))
                : UserErrors.InvalidCredentials);
        }

        user.RegisterSuccessfulSignIn();

        return user;
    }

    /// <summary>
    /// Records the successful sign-in; it is saved with the caller's own changes (or here when there are none).
    /// </summary>
    public async Task SucceedAsync(User user, CancellationToken cancellationToken)
    {
        Record(SignedIn, "Signed in", user);
        await SaveAsync(cancellationToken);
    }

    /// <summary>
    /// The password was right but the account may not sign in (inactive, no menu access).
    /// </summary>
    public async Task<Result<T>> RejectAsync<T>(User user, Error error, CancellationToken cancellationToken)
    {
        Record(SignInRejected, $"Sign-in refused: {error.Description}", user);
        await SaveAsync(cancellationToken);

        return Result.Failure<T>(error);
    }

    private void Record(string action, string summary, User? user, string? email = null) =>
        auditTrail.Record(new AuditEntry(AuditCategory.SignIn, action, summary)
        {
            EntityType = user is null ? null : nameof(User),
            EntityId = user?.Id,
            UserId = user?.Id,
            UserEmail = user?.Email ?? email
        });

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Two sign-ins of the same account at the same moment: the other one already counted this round.
        }
    }

    private static int RemainingMinutes(User user, DateTime now) =>
        Math.Max(1, (int)Math.Ceiling((user.LockoutEndUtc!.Value - now).TotalMinutes));
}
