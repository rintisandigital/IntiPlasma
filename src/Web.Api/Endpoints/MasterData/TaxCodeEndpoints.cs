using Application.Abstractions.Messaging;
using Application.TaxCodes;
using Application.TaxCodes.Create;
using Application.TaxCodes.Get;
using Application.TaxCodes.Update;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.MasterData;

internal sealed class TaxCodeEndpoints : IEndpoint
{
    public sealed record UpdateRequest(string Name, bool IsActive, IReadOnlyList<TaxRateRequest> Rates);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("tax-codes").WithTags(Tags.TaxCodes);

        group.MapGet("", async (
            IQueryHandler<GetTaxCodesQuery, IReadOnlyList<TaxCodeResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<TaxCodeResponse>> result = await handler.Handle(new GetTaxCodesQuery(), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.MasterDataRead);

        group.MapPost("", async (
            CreateTaxCodeCommand command,
            ICommandHandler<CreateTaxCodeCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.MasterDataManage)
        .WithIdempotency();

        group.MapPut("{taxCodeId:guid}", async (
            Guid taxCodeId,
            UpdateRequest request,
            ICommandHandler<UpdateTaxCodeCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateTaxCodeCommand(taxCodeId, request.Name, request.IsActive, request.Rates);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.MasterDataManage);
    }
}
