using Application.Abstractions.Messaging;
using Application.Users.FieldOfficers;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

/// <summary>
/// PPL lookup (PLAN-MOBILE §4.1): field officers a farmer or coop can be assigned to, and the PPL filter of the
/// Manager's lists.
/// </summary>
internal sealed class FieldOfficerEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("users/field-officers", async (
            Guid? branchId,
            IQueryHandler<GetFieldOfficersQuery, IReadOnlyList<FieldOfficerResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<FieldOfficerResponse>> result =
                await handler.Handle(new GetFieldOfficersQuery(branchId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .HasPermission(Permissions.FarmersRead);
    }
}
