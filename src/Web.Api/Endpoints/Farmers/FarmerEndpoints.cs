using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Common;
using Application.Farmers;
using Application.Farmers.Create;
using Application.Farmers.Get;
using Application.Farmers.GetById;
using Application.Farmers.Update;
using Domain.MasterData.Farmers;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Farmers;

internal sealed class FarmerEndpoints : IEndpoint
{
    public sealed record UpdateRequest(
        string Name,
        string? Nik,
        TaxIdentityRequest TaxIdentity,
        string? Address,
        string? Phone,
        BankAccountRequest BankAccount,
        bool IsActive,
        IReadOnlyList<Guid>? Documents = null,
        Guid? FieldOfficerUserId = null);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("farmers").WithTags(Tags.Farmers);

        group.MapGet("", async (
            string? search,
            int? page,
            int? pageSize,
            Guid? branchId,
            FarmerType? type,
            Guid? fieldOfficerId,
            IQueryHandler<GetFarmersQuery, PagedList<FarmerResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<PagedList<FarmerResponse>> result = await handler.Handle(
                new GetFarmersQuery(new PageRequest(page, pageSize, search), branchId, type, fieldOfficerId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.FarmersRead);

        group.MapGet("{farmerId:guid}", async (
            Guid farmerId,
            IQueryHandler<GetFarmerByIdQuery, FarmerResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<FarmerResponse> result = await handler.Handle(new GetFarmerByIdQuery(farmerId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.FarmersRead);

        group.MapPost("", async (
            CreateFarmerCommand command,
            ICommandHandler<CreateFarmerCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.FarmersManage)
        .WithIdempotency();

        group.MapPut("{farmerId:guid}", async (
            Guid farmerId,
            UpdateRequest request,
            ICommandHandler<UpdateFarmerCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateFarmerCommand(
                farmerId,
                request.Name,
                request.Nik,
                request.TaxIdentity,
                request.Address,
                request.Phone,
                request.BankAccount,
                request.IsActive,
                request.Documents,
                request.FieldOfficerUserId);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.FarmersManage);
    }
}
