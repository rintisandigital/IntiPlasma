using System.Reflection;
using System.Text;
using Application.Abstractions.Auditing;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Auditing;
using SharedKernel;

namespace Application.Abstractions.Behaviors;

/// <summary>
/// Records successful <see cref="IAuditedCommand"/>s (access changes) in the audit trail, whether they come from
/// Web.App or Web.Api. Failed commands change nothing and are not recorded.
/// </summary>
internal static class AuditDecorator
{
    internal sealed class CommandHandler<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> innerHandler,
        IAuditTrail auditTrail,
        IApplicationDbContext context)
        : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
        {
            Result<TResponse> result = await innerHandler.Handle(command, cancellationToken);

            if (result.IsSuccess && command is IAuditedCommand audited)
            {
                Guid? createdId = result.Value is Guid id ? id : null;
                auditTrail.Record(CreateEntry(audited, createdId));
                await context.SaveChangesAsync(cancellationToken);
            }

            return result;
        }
    }

    internal sealed class CommandBaseHandler<TCommand>(
        ICommandHandler<TCommand> innerHandler,
        IAuditTrail auditTrail,
        IApplicationDbContext context)
        : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public async Task<Result> Handle(TCommand command, CancellationToken cancellationToken)
        {
            Result result = await innerHandler.Handle(command, cancellationToken);

            if (result.IsSuccess && command is IAuditedCommand audited)
            {
                auditTrail.Record(CreateEntry(audited, null));
                await context.SaveChangesAsync(cancellationToken);
            }

            return result;
        }
    }

    internal static AuditEntry CreateEntry(IAuditedCommand command, Guid? createdId)
    {
        string action = ActionName(command.GetType());

        Guid? entityId = createdId ?? command.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(Guid))
            .Select(p => (Guid?)p.GetValue(command))
            .FirstOrDefault();

        return new AuditEntry(AuditCategory.Access, action, $"{Humanize(action)} ({command.AuditEntityType})")
        {
            EntityType = command.AuditEntityType,
            EntityId = entityId,
            Details = command
        };
    }

    private static string ActionName(Type commandType)
    {
        const string suffix = "Command";
        string name = commandType.Name;

        return name.EndsWith(suffix, StringComparison.Ordinal) ? name[..^suffix.Length] : name;
    }

    /// <summary>
    /// <c>SetUserAccess</c> → <c>Set user access</c>.
    /// </summary>
    private static string Humanize(string pascalCase)
    {
        var builder = new StringBuilder(pascalCase.Length + 8);

        foreach (char c in pascalCase)
        {
            if (char.IsUpper(c) && builder.Length > 0)
            {
                builder.Append(' ').Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
