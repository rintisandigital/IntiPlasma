using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Contracts;
using Application.Contracts.ChangeStatus;
using Application.Contracts.Create;
using Application.Contracts.Get;
using Application.Contracts.GetById;
using Application.Contracts.Update;
using Domain.Partnership.Contracts;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Partnership;

internal sealed class ContractEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("contracts").WithTags(Tags.Contracts);

        group.MapGet("", async (
            string? search,
            int? page,
            int? pageSize,
            Guid? branchId,
            ContractStatus? status,
            IQueryHandler<GetContractsQuery, PagedList<ContractResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<PagedList<ContractResponse>> result = await handler.Handle(
                new GetContractsQuery(new PageRequest(page, pageSize, search), branchId, status), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.ContractsRead);

        group.MapGet("{contractId:guid}", async (
            Guid contractId,
            IQueryHandler<GetContractByIdQuery, ContractResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<ContractResponse> result = await handler.Handle(new GetContractByIdQuery(contractId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.ContractsRead);

        group.MapPost("", async (
            CreateContractCommand command,
            ICommandHandler<CreateContractCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.ContractsManage)
        .WithIdempotency();

        group.MapPut("{contractId:guid}", async (
            Guid contractId,
            ContractTermsRequest terms,
            ICommandHandler<UpdateContractCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new UpdateContractCommand(contractId, terms), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.ContractsManage);

        group.MapPost("{contractId:guid}/activate", async (
            Guid contractId,
            ICommandHandler<ActivateContractCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new ActivateContractCommand(contractId), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.ContractsManage);

        group.MapPost("{contractId:guid}/deactivate", async (
            Guid contractId,
            ICommandHandler<DeactivateContractCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new DeactivateContractCommand(contractId), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.ContractsManage);
    }
}
