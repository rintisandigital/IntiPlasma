using SharedKernel;

namespace Web.App.Infrastructure.Workflow;

/// <summary>
/// Marks a controller action that approves, posts, voids, pays or closes a document. Every such action goes
/// through <see cref="IWorkflowActionService"/>, so the future centralized multi-level approval (W-4,
/// PLAN-WEBAPP §4.8) can take over in one place.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class WorkflowActionAttribute(string action) : Attribute
{
    public string Action { get; } = action;
}

/// <param name="DocumentType">E.g. "PurchaseOrder".</param>
/// <param name="Action">E.g. "approve", "post", "void".</param>
public sealed record WorkflowActionRequest(string DocumentType, Guid DocumentId, string Action);

public interface IWorkflowActionService
{
    Task<Result> ExecuteAsync(
        WorkflowActionRequest request,
        Func<CancellationToken, Task<Result>> execute,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Until the approval module exists, workflow actions run the document's own command directly; maker-checker
/// rules in the domain still apply.
/// </summary>
internal sealed class DirectWorkflowActionService : IWorkflowActionService
{
    public Task<Result> ExecuteAsync(
        WorkflowActionRequest request,
        Func<CancellationToken, Task<Result>> execute,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(execute);

        return execute(cancellationToken);
    }
}
