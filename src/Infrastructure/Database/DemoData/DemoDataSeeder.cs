using System.Data.Common;
using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using Application.Abstractions.Messaging;
using Dapper;
using Infrastructure.DomainEvents;
using Infrastructure.Outbox;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using SharedKernel;

namespace Infrastructure.Database.DemoData;

/// <summary>
/// Dummy data for demos, training and manual testing (configuration <c>Seed:DemoData=true</c>, docs/DEMO-DATA.md).
/// Every record goes through the real command handlers — validation, domain rules, branch access, document numbering,
/// maker-checker and the auto journals of the outbox — so the data is consistent across all modules. The operations
/// are simulated day by day over the last ~95 days up to yesterday: two branches, eight production cycles in every
/// stage (planned, active, harvesting, closed, settled), purchasing, stock, sales, receivables, payables, cash & bank,
/// manual journals and a bank reconciliation.
/// <para>
/// Runs only on a database without branches (after the base seed: chart of accounts, units and the Seed:Admin user),
/// never in Production. The outbox is processed inline after each command, so Web.App should not run meanwhile.
/// </para>
/// </summary>
public sealed partial class DemoDataSeeder
{
    /// <summary>
    /// Password of the demo users (checker and branch staff); demo data never runs in Production.
    /// </summary>
#pragma warning disable S2068 // Known demo credential, documented in docs/DEMO-DATA.md.
    public const string DemoPassword = "Demo123!";
#pragma warning restore S2068

    private const int HistoryDays = 95;

    private readonly IServiceProvider _services;
    private readonly ILogger _logger;
    private readonly CancellationToken _ct;
    private readonly DateOnly _today;
    private readonly DateOnly _start;
    private Guid _adminId;
    private Guid _checkerId;
    private int _commands;

    private DemoDataSeeder(IServiceProvider services, ILogger logger, CancellationToken cancellationToken)
    {
        _services = services;
        _logger = logger;
        _ct = cancellationToken;

        // UTC date: never later than the local date, so no document is dated in the future.
        _today = DateOnly.FromDateTime(services.GetRequiredService<IDateTimeProvider>().UtcNow);
        _start = _today.AddDays(-HistoryDays);
    }

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger<DemoDataSeeder>();

        var seeder = new DemoDataSeeder(services, logger, cancellationToken);

        await seeder.RunAsync();
    }

    private async Task RunAsync()
    {
        if (await QueryDbAsync(db => db.Branches.AnyAsync(_ct)))
        {
            _logger.LogInformation("Demo data skipped: the database already has branches");
            return;
        }

        string? adminEmail = _services.GetRequiredService<IConfiguration>()["Seed:Admin:Email"];
        _adminId = await QueryDbAsync(db => db.Users.Where(u => u.Email == adminEmail).Select(u => u.Id).SingleOrDefaultAsync(_ct));

        if (_adminId == Guid.Empty)
        {
            throw new InvalidOperationException("Demo data needs the Seed:Admin user; run the base seed first.");
        }

        _logger.LogInformation("Demo data: seeding {From} – {To}", _start, _today.AddDays(-1));

        await ProcessOutboxAsync();
        await SetUpAsync();

        for (DateOnly date = _start; date < _today; date = date.AddDays(1))
        {
            await RunFinanceDayAsync(date);

            foreach (CycleRun cycle in _cycles)
            {
                await cycle.RunDayAsync(date);
            }
        }

        await FinishAsync();

        _logger.LogInformation("Demo data: done ({Commands} commands)", _commands);
    }

    private Task<T> SendAsync<T>(ICommand<T> command) => SendAsAsync(_adminId, command);

    private Task SendAsync(ICommand command) => SendAsAsync(_adminId, command);

    private async Task<T> SendAsAsync<T>(Guid userId, ICommand<T> command)
    {
        Type handlerType = typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(T));

        Result<T> result = await HandleAsync<Result<T>>(userId, handlerType, command);

        EnsureSuccess(result, command);

        return result.Value;
    }

    private async Task SendAsAsync(Guid userId, ICommand command)
    {
        Type handlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());

        Result result = await HandleAsync<Result>(userId, handlerType, command);

        EnsureSuccess(result, command);
    }

    private async Task<T> QueryAsync<T>(IQuery<T> query)
    {
        Type handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(T));

        Result<T> result = await HandleAsync<Result<T>>(_adminId, handlerType, query);

        EnsureSuccess(result, query);

        return result.Value;
    }

    /// <summary>
    /// Runs a handler (with its decorators) in its own scope as the given user, then publishes the domain events it raised.
    /// </summary>
    private async Task<TResult> HandleAsync<TResult>(Guid userId, Type handlerType, object request)
    {
        Impersonate(userId);

        TResult result;

        using (IServiceScope scope = _services.CreateScope())
        {
            object handler = scope.ServiceProvider.GetRequiredService(handlerType);
            MethodInfo handle = handlerType.GetMethod("Handle")!;

            result = await (Task<TResult>)handle.Invoke(handler, [request, _ct])!;
        }

        _commands++;

        await ProcessOutboxAsync();

        return result;
    }

    /// <summary>
    /// The handlers read the current user from the HTTP context (IUserContext, branch access, maker-checker).
    /// </summary>
    private void Impersonate(Guid userId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            authenticationType: "DemoData");

        _services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };
    }

    private static void EnsureSuccess(Result result, object request)
    {
        if (result.IsSuccess)
        {
            return;
        }

        string details = result.Error is ValidationError validation
            ? string.Join("; ", validation.Errors.Select(e => $"{e.Code}: {e.Description}"))
            : $"{result.Error.Code}: {result.Error.Description}";

        throw new InvalidOperationException($"Demo data: {request.GetType().Name} failed — {details}");
    }

    private async Task<T> QueryDbAsync<T>(Func<ApplicationDbContext, Task<T>> query)
    {
        using IServiceScope scope = _services.CreateScope();

        return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    /// <summary>
    /// Same work as the OutboxProcessor (Web.App), but inline and failing loudly: the next command often depends on
    /// a handler (coop warehouse, auto journals).
    /// </summary>
    private async Task ProcessOutboxAsync()
    {
        NpgsqlDataSource dataSource = _services.GetRequiredService<NpgsqlDataSource>();
        IDomainEventsDispatcher dispatcher = _services.GetRequiredService<IDomainEventsDispatcher>();
        IDateTimeProvider dateTimeProvider = _services.GetRequiredService<IDateTimeProvider>();

        const string selectSql =
            """
            SELECT id AS Id, type AS Type, content AS Content
            FROM infrastructure.outbox_messages
            WHERE processed_on_utc IS NULL
            ORDER BY occurred_on_utc
            LIMIT 100
            FOR UPDATE SKIP LOCKED
            """;

        const string updateSql =
            """
            UPDATE infrastructure.outbox_messages
            SET processed_on_utc = @ProcessedOnUtc, attempts = attempts + 1, error = NULL
            WHERE id = @Id
            """;

        while (true)
        {
            await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(_ct);
            await using DbTransaction transaction = await connection.BeginTransactionAsync(_ct);

            var messages = (await connection.QueryAsync<OutboxRow>(
                new CommandDefinition(selectSql, transaction: transaction, cancellationToken: _ct))).ToList();

            if (messages.Count == 0)
            {
                return;
            }

            foreach (OutboxRow message in messages)
            {
                IDomainEvent domainEvent = DomainEventTypes.Deserialize(message.Type, message.Content);

                try
                {
                    await dispatcher.DispatchAsync([domainEvent], _ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new InvalidOperationException($"Demo data: domain event {message.Type} failed", ex);
                }

                await connection.ExecuteAsync(new CommandDefinition(
                    updateSql,
                    new { message.Id, ProcessedOnUtc = dateTimeProvider.UtcNow },
                    transaction,
                    cancellationToken: _ct));
            }

            await transaction.CommitAsync(_ct);
        }
    }

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);

    internal sealed class OutboxRow
    {
        public Guid Id { get; set; }

        public string Type { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;
    }
}
