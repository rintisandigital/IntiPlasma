using System.Collections.Concurrent;
using Application.Abstractions.Storage;

namespace Application.UnitTests.Abstractions;

public sealed class InMemoryFileStorage : IFileStorage
{
    public ConcurrentDictionary<string, byte[]> Files { get; } = new();

    public async Task SaveAsync(string path, Stream content, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        Files[path] = buffer.ToArray();
    }

    public Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream?>(Files.TryGetValue(path, out byte[]? bytes) ? new MemoryStream(bytes) : null);

    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        Files.TryRemove(path, out _);
        return Task.CompletedTask;
    }
}
