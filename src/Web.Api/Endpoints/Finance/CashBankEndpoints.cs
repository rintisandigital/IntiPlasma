using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Finance.CashBank;
using Domain.Finance.CashBank;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed class CashBankEndpoints : IEndpoint
{
    public sealed record UpdateAccountRequest(string Name, string? BankName, string? AccountNumber, bool IsActive);

    public sealed record ReasonRequest(string Reason);

    public sealed record StatementBalanceRequest(decimal StatementBalance);

    public sealed record StatementLinesRequest(IReadOnlyList<StatementLineRequest> Lines);

    public sealed record CsvRequest(string Csv);

    public sealed record MatchRequest(Guid JournalEntryId, int JournalLineNumber);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        MapAccounts(app.MapGroup("finance/cash-bank-accounts").WithTags(Tags.CashBankAccounts));
        MapTransactions(app.MapGroup("finance").WithTags(Tags.CashTransactions));
        MapReconciliations(app.MapGroup("finance/bank-reconciliations").WithTags(Tags.BankReconciliations));
    }

    private static void MapAccounts(RouteGroupBuilder group)
    {
        group.MapGet("", async (
            Guid? branchId,
            CashBankAccountType? type,
            bool? includeInactive,
            IQueryHandler<GetCashBankAccountsQuery, IReadOnlyList<CashBankAccountResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<CashBankAccountResponse>> result = await handler.Handle(
                new GetCashBankAccountsQuery(branchId, type, includeInactive ?? false), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.CashBankRead);

        group.MapPost("", async (
            CreateCashBankAccountCommand command,
            ICommandHandler<CreateCashBankAccountCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.CashBankManage)
        .WithIdempotency();

        group.MapPut("{cashBankAccountId:guid}", async (
            Guid cashBankAccountId,
            UpdateAccountRequest request,
            ICommandHandler<UpdateCashBankAccountCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new UpdateCashBankAccountCommand(cashBankAccountId, request.Name, request.BankName, request.AccountNumber, request.IsActive),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.CashBankManage);

        group.MapGet("{cashBankAccountId:guid}/ledger", async (
            Guid cashBankAccountId,
            DateOnly from,
            DateOnly to,
            IQueryHandler<GetCashBankLedgerQuery, CashBankLedgerResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<CashBankLedgerResponse> result = await handler.Handle(
                new GetCashBankLedgerQuery(cashBankAccountId, from, to), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.CashBankRead);
    }

    private static void MapTransactions(RouteGroupBuilder group)
    {
        group.MapGet("cash-transactions", async (
            string? search,
            int? page,
            int? pageSize,
            Guid? branchId,
            Guid? cashBankAccountId,
            CashDirection? direction,
            CashTransactionStatus? status,
            DateOnly? from,
            DateOnly? to,
            IQueryHandler<GetCashTransactionsQuery, PagedList<CashTransactionResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetCashTransactionsQuery(
                new PageRequest(page, pageSize, search), branchId, cashBankAccountId, direction, status, from, to);

            Result<PagedList<CashTransactionResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.CashBankRead);

        group.MapGet("cash-transactions/{cashTransactionId:guid}", async (
            Guid cashTransactionId,
            IQueryHandler<GetCashTransactionByIdQuery, CashTransactionResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<CashTransactionResponse> result = await handler.Handle(new GetCashTransactionByIdQuery(cashTransactionId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.CashBankRead);

        group.MapPost("cash-transactions", async (
            CreateCashTransactionCommand command,
            ICommandHandler<CreateCashTransactionCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(id => Results.Ok(new { Id = id }), CustomResults.Problem);
        })
        .HasPermission(Permissions.CashBankManage)
        .WithIdempotency();

        group.MapPost("cash-transactions/{cashTransactionId:guid}/approve", async (
            Guid cashTransactionId,
            ICommandHandler<ApproveCashTransactionCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new ApproveCashTransactionCommand(cashTransactionId), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.CashBankApprove);

        group.MapPost("cash-transactions/{cashTransactionId:guid}/post", async (
            Guid cashTransactionId,
            ICommandHandler<PostCashTransactionCommand, string> handler,
            CancellationToken cancellationToken) =>
        {
            Result<string> result = await handler.Handle(new PostCashTransactionCommand(cashTransactionId), cancellationToken);

            return result.Match(number => Results.Ok(new { Number = number }), CustomResults.Problem);
        })
        .HasPermission(Permissions.CashBankManage);

        group.MapPost("cash-transactions/{cashTransactionId:guid}/cancel", async (
            Guid cashTransactionId,
            ReasonRequest request,
            ICommandHandler<CancelCashTransactionCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new CancelCashTransactionCommand(cashTransactionId, request.Reason), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.CashBankManage);

        group.MapGet("bank-transfers", async (
            string? search,
            int? page,
            int? pageSize,
            Guid? branchId,
            Guid? cashBankAccountId,
            DateOnly? from,
            DateOnly? to,
            IQueryHandler<GetBankTransfersQuery, PagedList<BankTransferResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<PagedList<BankTransferResponse>> result = await handler.Handle(
                new GetBankTransfersQuery(new PageRequest(page, pageSize, search), branchId, cashBankAccountId, from, to), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.CashBankRead);

        group.MapPost("bank-transfers", async (
            CreateBankTransferCommand command,
            ICommandHandler<CreateBankTransferCommand, CreateBankTransferResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<CreateBankTransferResponse> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.CashBankManage)
        .WithIdempotency();
    }

    private static void MapReconciliations(RouteGroupBuilder group)
    {
        group.MapGet("", async (
            Guid? cashBankAccountId,
            Guid? branchId,
            IQueryHandler<GetBankReconciliationsQuery, IReadOnlyList<BankReconciliationSummary>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<BankReconciliationSummary>> result = await handler.Handle(
                new GetBankReconciliationsQuery(cashBankAccountId, branchId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.CashBankRead);

        group.MapGet("{bankReconciliationId:guid}", async (
            Guid bankReconciliationId,
            IQueryHandler<GetBankReconciliationByIdQuery, BankReconciliationResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<BankReconciliationResponse> result = await handler.Handle(
                new GetBankReconciliationByIdQuery(bankReconciliationId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.CashBankRead);

        group.MapPost("", async (
            StartBankReconciliationCommand command,
            ICommandHandler<StartBankReconciliationCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(id => Results.Ok(new { Id = id }), CustomResults.Problem);
        })
        .HasPermission(Permissions.CashBankReconcile);

        group.MapPut("{bankReconciliationId:guid}/statement-balance", async (
            Guid bankReconciliationId,
            StatementBalanceRequest request,
            ICommandHandler<UpdateStatementBalanceCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new UpdateStatementBalanceCommand(bankReconciliationId, request.StatementBalance), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.CashBankReconcile);

        group.MapPost("{bankReconciliationId:guid}/statement-lines", async (
            Guid bankReconciliationId,
            StatementLinesRequest request,
            ICommandHandler<AddStatementLinesCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new AddStatementLinesCommand(bankReconciliationId, request.Lines), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.CashBankReconcile);

        group.MapPost("{bankReconciliationId:guid}/statement-lines/import", async (
            Guid bankReconciliationId,
            CsvRequest request,
            ICommandHandler<ImportStatementLinesCommand, int> handler,
            CancellationToken cancellationToken) =>
        {
            Result<int> result = await handler.Handle(new ImportStatementLinesCommand(bankReconciliationId, request.Csv), cancellationToken);

            return result.Match(count => Results.Ok(new { Imported = count }), CustomResults.Problem);
        })
        .HasPermission(Permissions.CashBankReconcile);

        group.MapDelete("{bankReconciliationId:guid}/statement-lines/{lineNumber:int}", async (
            Guid bankReconciliationId,
            int lineNumber,
            ICommandHandler<RemoveStatementLineCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new RemoveStatementLineCommand(bankReconciliationId, lineNumber), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.CashBankReconcile);

        group.MapPost("{bankReconciliationId:guid}/statement-lines/{lineNumber:int}/match", async (
            Guid bankReconciliationId,
            int lineNumber,
            MatchRequest request,
            ICommandHandler<MatchStatementLineCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new MatchStatementLineCommand(bankReconciliationId, lineNumber, request.JournalEntryId, request.JournalLineNumber),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.CashBankReconcile);

        group.MapPost("{bankReconciliationId:guid}/statement-lines/{lineNumber:int}/unmatch", async (
            Guid bankReconciliationId,
            int lineNumber,
            ICommandHandler<UnmatchStatementLineCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new UnmatchStatementLineCommand(bankReconciliationId, lineNumber), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.CashBankReconcile);

        group.MapPost("{bankReconciliationId:guid}/auto-match", async (
            Guid bankReconciliationId,
            ICommandHandler<AutoMatchStatementLinesCommand, int> handler,
            CancellationToken cancellationToken) =>
        {
            Result<int> result = await handler.Handle(new AutoMatchStatementLinesCommand(bankReconciliationId), cancellationToken);

            return result.Match(count => Results.Ok(new { Matched = count }), CustomResults.Problem);
        })
        .HasPermission(Permissions.CashBankReconcile);

        group.MapPost("{bankReconciliationId:guid}/complete", async (
            Guid bankReconciliationId,
            ICommandHandler<CompleteBankReconciliationCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new CompleteBankReconciliationCommand(bankReconciliationId), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.CashBankReconcile);
    }
}
