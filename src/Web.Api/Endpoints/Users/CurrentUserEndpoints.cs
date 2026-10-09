using Application.Abstractions.Messaging;
using Application.Users.ChangePassword;
using Application.Users.GetCurrent;
using Application.Users.Logout;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

/// <summary>
/// The signed-in user's own session (PLAN-MOBILE §4.1): profile with roles and permissions, logout of one device,
/// and changing their own password. Any authenticated user may call these; no permission is required.
/// </summary>
internal sealed class CurrentUserEndpoints : IEndpoint
{
    public sealed record LogoutRequest(string RefreshToken);

    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("users/me", async (
            IQueryHandler<GetCurrentUserQuery, CurrentUserResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<CurrentUserResponse> result = await handler.Handle(new GetCurrentUserQuery(), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .RequireAuthorization();

        app.MapPost("users/logout", async (
            LogoutRequest request,
            ICommandHandler<LogoutUserCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new LogoutUserCommand(request.RefreshToken), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .RequireAuthorization();

        app.MapPost("users/me/change-password", async (
            ChangePasswordRequest request,
            ICommandHandler<ChangeOwnPasswordCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new ChangeOwnPasswordCommand(request.CurrentPassword, request.NewPassword),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .RequireAuthorization()
        .RequireRateLimiting(RateLimitingPolicies.Authentication);
    }
}
