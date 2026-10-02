namespace Infrastructure.BackgroundJobs;

/// <summary>
/// Background jobs (outbox processing, attachment cleanup) run in the host that enables them: Web.App in
/// normal deployments, Web.Api only when it runs without Web.App (e.g. API-only development).
/// </summary>
internal sealed class BackgroundJobsOptions
{
    public const string SectionName = "BackgroundJobs";

    public bool Enabled { get; init; }
}
