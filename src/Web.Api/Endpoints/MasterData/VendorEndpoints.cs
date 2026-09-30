using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Common;
using Application.Vendors;
using Application.Vendors.Create;
using Application.Vendors.Get;
using Application.Vendors.GetById;
using Application.Vendors.Update;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.MasterData;

internal sealed class VendorEndpoints : IEndpoint
{
    public sealed record UpdateRequest(
        string Name,
        TaxIdentityRequest TaxIdentity,
        string? Address,
        string? Phone,
        string? Email,
        int PaymentTermDays,
        BankAccountRequest BankAccount,
        bool IsActive);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("vendors").WithTags(Tags.Vendors);

        group.MapGet("", async (
            string? search,
            int? page,
            int? pageSize,
            IQueryHandler<GetVendorsQuery, PagedList<VendorResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<PagedList<VendorResponse>> result = await handler.Handle(
                new GetVendorsQuery(new PageRequest(page, pageSize, search)), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.MasterDataRead);

        group.MapGet("{vendorId:guid}", async (
            Guid vendorId,
            IQueryHandler<GetVendorByIdQuery, VendorResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<VendorResponse> result = await handler.Handle(new GetVendorByIdQuery(vendorId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.MasterDataRead);

        group.MapPost("", async (
            CreateVendorCommand command,
            ICommandHandler<CreateVendorCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.MasterDataManage)
        .WithIdempotency();

        group.MapPut("{vendorId:guid}", async (
            Guid vendorId,
            UpdateRequest request,
            ICommandHandler<UpdateVendorCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateVendorCommand(
                vendorId,
                request.Name,
                request.TaxIdentity,
                request.Address,
                request.Phone,
                request.Email,
                request.PaymentTermDays,
                request.BankAccount,
                request.IsActive);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.MasterDataManage);
    }
}
