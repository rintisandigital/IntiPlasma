namespace MobileApp.Core.Production;

/// <summary>
/// Photos of queued entries, kept on the device until they are uploaded (PLAN-MOBILE M-44). Each file is named after
/// its attachment id.
/// </summary>
public sealed class PendingFiles(string directory)
{
    public string Directory => directory;

    public string PathFor(Guid attachmentId) => Path.Combine(directory, $"{attachmentId:N}.jpg");

    public bool Exists(Guid attachmentId) => File.Exists(PathFor(attachmentId));

    /// <summary>
    /// A new, empty location for a photo; the folder is created when missing.
    /// </summary>
    public string Prepare(Guid attachmentId)
    {
        System.IO.Directory.CreateDirectory(directory);

        return PathFor(attachmentId);
    }

    public async Task<byte[]?> ReadAsync(Guid attachmentId, CancellationToken cancellationToken = default) =>
        Exists(attachmentId) ? await File.ReadAllBytesAsync(PathFor(attachmentId), cancellationToken) : null;

    public void Delete(Guid attachmentId)
    {
        string path = PathFor(attachmentId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public void DeleteAll(IEnumerable<Guid> attachmentIds)
    {
        foreach (Guid id in attachmentIds)
        {
            Delete(id);
        }
    }
}
