namespace Application.Abstractions.Storage;

/// <summary>
/// Physical storage of uploaded files (local disk now, an object store such as S3/MinIO later).
/// Paths are relative to the storage root and use forward slashes.
/// </summary>
public interface IFileStorage
{
    Task SaveAsync(string path, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the file for reading, or returns null when it does not exist.
    /// </summary>
    Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the file; a missing file is not an error.
    /// </summary>
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);
}
