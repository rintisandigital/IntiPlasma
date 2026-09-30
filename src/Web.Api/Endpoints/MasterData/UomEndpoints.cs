using Application.Abstractions.Messaging;
using Application.Uoms.Create;
using Application.Uoms.Get;
using Application.Uoms.Update;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.MasterData;

internal sealed class UomEndpoints : IEndpoint
{
    public sealed record UpdateRequest(string Name);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("uoms").WithTags(Tags.Uoms);

        group.MapGet("", async (
            IQueryHandler<GetUomsQuery, IReadOnlyList<UomResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<UomResponse>> result = await handler.Handle(new GetUomsQuery(), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.MasterDataRead);

        group.MapPost("", async (
            CreateUomCommand command,
            ICommandHandler<CreateUomCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.MasterDataManage)
        .WithIdempotency();

        group.MapPut("{uomId:guid}", async (
            Guid uomId,
            UpdateRequest request,
            ICommandHandler<UpdateUomCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new UpdateUomCommand(uomId, request.Name), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.MasterDataManage);
    }
}
