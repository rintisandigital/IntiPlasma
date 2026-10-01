using Application.Abstractions.Messaging;
using Application.Finance.Reports;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed class FinanceReportEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("finance/reports").WithTags(Tags.FinanceReports);

        group.MapGet("general-ledger", async (
            Guid accountId,
            DateOnly from,
            DateOnly to,
            Guid? branchId,
            Guid? costCenterId,
            IQueryHandler<GetGeneralLedgerQuery, GeneralLedgerResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<GeneralLedgerResponse> result = await handler.Handle(
                new GetGeneralLedgerQuery(accountId, from, to, branchId, costCenterId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.FinanceReportsRead);

        group.MapGet("trial-balance", async (
            DateOnly from,
            DateOnly to,
            Guid? branchId,
            bool? includeZeroBalances,
            IQueryHandler<GetTrialBalanceQuery, TrialBalanceResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<TrialBalanceResponse> result = await handler.Handle(
                new GetTrialBalanceQuery(from, to, branchId, includeZeroBalances ?? false), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.FinanceReportsRead);

        group.MapGet("income-statement", async (
            DateOnly from,
            DateOnly to,
            Guid? branchId,
            IQueryHandler<GetIncomeStatementQuery, IncomeStatementResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IncomeStatementResponse> result = await handler.Handle(new GetIncomeStatementQuery(from, to, branchId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.FinanceReportsRead);

        group.MapGet("balance-sheet", async (
            DateOnly asOf,
            Guid? branchId,
            IQueryHandler<GetBalanceSheetQuery, BalanceSheetResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<BalanceSheetResponse> result = await handler.Handle(new GetBalanceSheetQuery(asOf, branchId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.FinanceReportsRead);

        group.MapGet("cash-flow", async (
            DateOnly from,
            DateOnly to,
            Guid? branchId,
            IQueryHandler<GetCashFlowStatementQuery, CashFlowStatementResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<CashFlowStatementResponse> result = await handler.Handle(new GetCashFlowStatementQuery(from, to, branchId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.FinanceReportsRead);

        group.MapGet("profitability", async (
            DateOnly from,
            DateOnly to,
            ProfitabilityGrouping? groupBy,
            Guid? branchId,
            Guid? farmerId,
            IQueryHandler<GetProfitabilityQuery, ProfitabilityResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<ProfitabilityResponse> result = await handler.Handle(
                new GetProfitabilityQuery(from, to, groupBy ?? ProfitabilityGrouping.Cycle, branchId, farmerId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.FinanceReportsRead);
    }
}
