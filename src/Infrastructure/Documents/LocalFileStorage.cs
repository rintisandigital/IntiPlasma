using Application.Abstractions.Storage;
using Microsoft.Extensions.Options;

namespace Infrastructure.Documents;

/// <summary>
/// Stores files on the local disk (a Docker volume in production). Fine for a single instance; use an object
/// store before scaling out.
/// </summary>
internal sealed class LocalFileStorage(IOptions<FileStorageOptions> options) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.RootPath, AppContext.BaseDirectory);

    public async Task SaveAsync(string path, Stream content, CancellationToken cancellationToken = default)
    {
        string fullPath = Resolve(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        string temporaryPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";

        await using (var file = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await content.CopyToAsync(file, cancellationToken);
        }

        File.Move(temporaryPath, fullPath, overwrite: true);
    }

    public Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken = default)
    {
        string fullPath = Resolve(path);

        Stream? stream = File.Exists(fullPath)
            ? new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;

        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        File.Delete(Resolve(path));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Paths come from the attachment metadata (never from the client), but are still confined to the root.
    /// </summary>
    private string Resolve(string path)
    {
        string fullPath = Path.GetFullPath(Path.Combine(_root, path));

        if (!fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"The storage path '{path}' is outside the storage root.");
        }

        return fullPath;
    }
}
