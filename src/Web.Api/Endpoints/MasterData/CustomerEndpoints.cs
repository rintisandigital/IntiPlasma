using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Common;
using Application.Customers;
using Application.Customers.Create;
using Application.Customers.Get;
using Application.Customers.GetById;
using Application.Customers.Update;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.MasterData;

internal sealed class CustomerEndpoints : IEndpoint
{
    public sealed record UpdateRequest(
        string Name,
        TaxIdentityRequest TaxIdentity,
        string? Address,
        string? Phone,
        string? Email,
        int PaymentTermDays,
        decimal CreditLimit,
        bool IsActive,
        IReadOnlyList<Guid>? Documents = null);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("customers").WithTags(Tags.Customers);

        group.MapGet("", async (
            string? search,
            int? page,
            int? pageSize,
            IQueryHandler<GetCustomersQuery, PagedList<CustomerResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<PagedList<CustomerResponse>> result = await handler.Handle(
                new GetCustomersQuery(new PageRequest(page, pageSize, search)), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.MasterDataRead);

        group.MapGet("{customerId:guid}", async (
            Guid customerId,
            IQueryHandler<GetCustomerByIdQuery, CustomerResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<CustomerResponse> result = await handler.Handle(new GetCustomerByIdQuery(customerId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.MasterDataRead);

        group.MapPost("", async (
            CreateCustomerCommand command,
            ICommandHandler<CreateCustomerCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.MasterDataManage)
        .WithIdempotency();

        group.MapPut("{customerId:guid}", async (
            Guid customerId,
            UpdateRequest request,
            ICommandHandler<UpdateCustomerCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateCustomerCommand(
                customerId,
                request.Name,
                request.TaxIdentity,
                request.Address,
                request.Phone,
                request.Email,
                request.PaymentTermDays,
                request.CreditLimit,
                request.IsActive,
                request.Documents);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.MasterDataManage);
    }
}
