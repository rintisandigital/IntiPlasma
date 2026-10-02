namespace Application.Abstractions.Caching;

/// <summary>
/// Removes cached entries in every running process (Web.Api, Web.App and their replicas), not only in the
/// process that executed the command. Call it after the change has been saved.
/// </summary>
public interface ICacheInvalidator
{
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default);
}
