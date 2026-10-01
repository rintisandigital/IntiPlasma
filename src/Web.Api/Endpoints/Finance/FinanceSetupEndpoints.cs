using Application.Abstractions.Messaging;
using Application.Finance.Accounts;
using Application.Finance.CostCenters;
using Application.Finance.FiscalPeriods;
using Application.Finance.JournalTemplates;
using Domain.Finance.Accounts;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

/// <summary>
/// Chart of accounts, cost centers, fiscal periods and journal templates.
/// </summary>
internal sealed class FinanceSetupEndpoints : IEndpoint
{
    public sealed record UpdateAccountRequest(string Name, bool IsActive, CashFlowCategory? CashFlowCategory = null);

    public sealed record UpdateCostCenterRequest(string Name, bool IsActive);

    public sealed record UpdateJournalTemplateRequest(
        string Name,
        string? Description,
        bool IsActive,
        IReadOnlyList<JournalTemplateLineRequest> Lines);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        MapAccounts(app.MapGroup("finance/accounts").WithTags(Tags.Accounts));
        MapCostCenters(app.MapGroup("finance/cost-centers").WithTags(Tags.CostCenters));
        MapFiscalPeriods(app.MapGroup("finance/fiscal-periods").WithTags(Tags.FiscalPeriods));
        MapJournalTemplates(app.MapGroup("finance/journal-templates").WithTags(Tags.JournalTemplates));
    }

    private static void MapAccounts(RouteGroupBuilder group)
    {
        group.MapGet("", async (
            AccountType? type,
            string? search,
            bool? postableOnly,
            IQueryHandler<GetAccountsQuery, IReadOnlyList<AccountResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<AccountResponse>> result = await handler.Handle(
                new GetAccountsQuery(type, search, postableOnly ?? false), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.FinanceSetupRead);

        group.MapPost("", async (
            CreateAccountCommand command,
            ICommandHandler<CreateAccountCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.FinanceSetupManage)
        .WithIdempotency();

        group.MapPut("{accountId:guid}", async (
            Guid accountId,
            UpdateAccountRequest request,
            ICommandHandler<UpdateAccountCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new UpdateAccountCommand(accountId, request.Name, request.IsActive, request.CashFlowCategory), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.FinanceSetupManage);
    }

    private static void MapCostCenters(RouteGroupBuilder group)
    {
        group.MapGet("", async (
            IQueryHandler<GetCostCentersQuery, IReadOnlyList<CostCenterResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<CostCenterResponse>> result = await handler.Handle(new GetCostCentersQuery(), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.FinanceSetupRead);

        group.MapPost("", async (
            CreateCostCenterCommand command,
            ICommandHandler<CreateCostCenterCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.FinanceSetupManage)
        .WithIdempotency();

        group.MapPut("{costCenterId:guid}", async (
            Guid costCenterId,
            UpdateCostCenterRequest request,
            ICommandHandler<UpdateCostCenterCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new UpdateCostCenterCommand(costCenterId, request.Name, request.IsActive), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.FinanceSetupManage);
    }

    private static void MapFiscalPeriods(RouteGroupBuilder group)
    {
        group.MapGet("", async (
            int? year,
            IQueryHandler<GetFiscalPeriodsQuery, IReadOnlyList<FiscalPeriodResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<FiscalPeriodResponse>> result = await handler.Handle(
                new GetFiscalPeriodsQuery(year), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.FinanceSetupRead);

        group.MapPost("years/{year:int}", async (
            int year,
            ICommandHandler<OpenFiscalYearCommand, int> handler,
            CancellationToken cancellationToken) =>
        {
            Result<int> result = await handler.Handle(new OpenFiscalYearCommand(year), cancellationToken);

            return result.Match(count => Results.Ok(new { year, periods = count }), CustomResults.Problem);
        })
        .HasPermission(Permissions.FinanceSetupManage);

        group.MapPost("{fiscalPeriodId:guid}/close", async (
            Guid fiscalPeriodId,
            ICommandHandler<CloseFiscalPeriodCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new CloseFiscalPeriodCommand(fiscalPeriodId), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.FiscalPeriodsClose);

        group.MapPost("{fiscalPeriodId:guid}/reopen", async (
            Guid fiscalPeriodId,
            ICommandHandler<ReopenFiscalPeriodCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new ReopenFiscalPeriodCommand(fiscalPeriodId), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.FiscalPeriodsClose);
    }

    private static void MapJournalTemplates(RouteGroupBuilder group)
    {
        group.MapGet("", async (
            IQueryHandler<GetJournalTemplatesQuery, IReadOnlyList<JournalTemplateResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<JournalTemplateResponse>> result = await handler.Handle(
                new GetJournalTemplatesQuery(), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.JournalsRead);

        group.MapPost("", async (
            CreateJournalTemplateCommand command,
            ICommandHandler<CreateJournalTemplateCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.FinanceSetupManage)
        .WithIdempotency();

        group.MapPut("{journalTemplateId:guid}", async (
            Guid journalTemplateId,
            UpdateJournalTemplateRequest request,
            ICommandHandler<UpdateJournalTemplateCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateJournalTemplateCommand(
                journalTemplateId, request.Name, request.Description, request.IsActive, request.Lines);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.FinanceSetupManage);
    }
}
