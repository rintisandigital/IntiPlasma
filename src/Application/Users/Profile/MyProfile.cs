using System.Data.Common;
using Application.Abstractions.Auditing;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Documents;
using Dapper;
using Domain.Documents.Attachments;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Profile;

/// <summary>
/// The signed-in user's own profile: identity, access profiles, roles and photo.
/// </summary>
public sealed record GetMyProfileQuery : IQuery<MyProfileResponse>;

public sealed record MyProfileResponse
{
    public Guid Id { get; init; }

    public string Email { get; init; }

    public string FirstName { get; init; }

    public string LastName { get; init; }

    public Guid? PhotoId { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public string? MenuAccessProfileName { get; init; }

    public string? BranchAccessProfileName { get; init; }

    public string? DefaultBranchName { get; init; }

    /// <summary>
    /// API roles of the user, ordered by name.
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; } = [];
}

internal sealed class GetMyProfileQueryHandler(IDbConnectionFactory dbConnectionFactory, IUserContext userContext)
    : IQueryHandler<GetMyProfileQuery, MyProfileResponse>
{
    public async Task<Result<MyProfileResponse>> Handle(GetMyProfileQuery query, CancellationToken cancellationToken)
    {
        Guid userId = userContext.UserId;

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string sql =
            """
            SELECT u.id AS Id, u.email AS Email, u.first_name AS FirstName, u.last_name AS LastName, u.photo_id AS PhotoId,
                   u.created_at_utc AS CreatedAtUtc, mp.name AS MenuAccessProfileName, bp.name AS BranchAccessProfileName,
                   b.code || ' — ' || b.name AS DefaultBranchName
            FROM identity.users u
            LEFT JOIN identity.menu_access_profiles mp ON mp.id = u.menu_access_profile_id
            LEFT JOIN identity.branch_access_profiles bp ON bp.id = u.branch_access_profile_id
            LEFT JOIN master.branches b ON b.id = u.default_branch_id
            WHERE u.id = @UserId;

            SELECT r.name
            FROM identity.user_roles ur
            JOIN identity.roles r ON r.id = ur.role_id
            WHERE ur.user_id = @UserId
            ORDER BY r.name;
            """;

        await using SqlMapper.GridReader grid = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken));

        MyProfileResponse? profile = await grid.ReadSingleOrDefaultAsync<MyProfileResponse>();
        IEnumerable<string> roles = await grid.ReadAsync<string>();

        return profile is null
            ? Result.Failure<MyProfileResponse>(UserErrors.NotFound(userId))
            : profile with { Roles = [.. roles] };
    }
}

/// <summary>
/// The signed-in user sets (an uploaded image attachment) or removes (null) their profile photo. The previous photo is
/// released and purged like any unused attachment.
/// </summary>
public sealed record SetMyPhotoCommand(Guid? PhotoId) : ICommand, IAuditedCommand
{
    string IAuditedCommand.AuditEntityType => "User";
}

internal sealed class SetMyPhotoCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IAttachmentService attachments) : ICommandHandler<SetMyPhotoCommand>
{
    public async Task<Result> Handle(SetMyPhotoCommand command, CancellationToken cancellationToken)
    {
        Guid userId = userContext.UserId;

        User? user = await context.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(userId));
        }

        if (command.PhotoId is { } photoId)
        {
            Result<AttachmentResponse> photo = await attachments.GetAsync(photoId, cancellationToken);
            if (photo.IsFailure)
            {
                return photo;
            }

            if (photo.Value.Kind != nameof(AttachmentKind.Photo))
            {
                return Result.Failure(UserErrors.PhotoNotImage);
            }
        }

        // A user is company-wide: the photo has no branch, so every user can see it.
        Result linked = await attachments.SyncLinksAsync(
            AttachmentOwner.Of(AttachmentOwnerTypes.User, userId),
            null,
            command.PhotoId is { } id ? [id] : [],
            cancellationToken);

        if (linked.IsFailure)
        {
            return linked;
        }

        user.SetPhoto(command.PhotoId);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
