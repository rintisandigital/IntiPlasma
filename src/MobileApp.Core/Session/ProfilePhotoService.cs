using System.Text.Json;
using MobileApp.Core.Api;
using MobileApp.Core.Contracts;
using MobileApp.Core.Local;

namespace MobileApp.Core.Session;

/// <summary>
/// Profile photo of the signed-in user (<c>users/me/photo</c>). The photo is cached in the session store of the local
/// database, so the avatar also shows offline; it is downloaded again only when <see cref="CurrentUser.PhotoId"/>
/// changes and is forgotten on logout together with the session.
/// </summary>
public sealed class ProfilePhotoService(UsersApi users, SessionService session, LocalDb db)
{
    internal const string CacheKey = "profile_photo";

    /// <summary>
    /// Raised after the photo was changed or removed on this device.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// The photo as a <c>data:</c> URL for an <c>img</c>, or null when the user has none or it cannot be loaded
    /// (the page then shows the initials).
    /// </summary>
    public async Task<string?> GetDataUrlAsync(CancellationToken cancellationToken = default)
    {
        if (session.User?.PhotoId is not { } photoId)
        {
            return null;
        }

        if (await ReadCacheAsync(cancellationToken) is { } cached && cached.PhotoId == photoId)
        {
            return cached.DataUrl;
        }

        ApiResult<AttachmentContent> photo = await users.GetPhotoAsync(cancellationToken);
        if (!photo.IsSuccess)
        {
            return null;
        }

        var downloaded = CachedPhoto.From(photoId, photo.Value.ContentType, photo.Value.Bytes);
        await WriteCacheAsync(downloaded, cancellationToken);

        return downloaded.DataUrl;
    }

    /// <summary>
    /// Uploads <paramref name="jpeg"/> as the new photo, then reloads the profile (new <c>photoId</c>).
    /// </summary>
    public async Task<ApiResult> UploadAsync(byte[] jpeg, CancellationToken cancellationToken = default)
    {
        ApiResult<UploadedPhoto> uploaded = await users.UploadPhotoAsync(jpeg, cancellationToken);
        if (!uploaded.IsSuccess)
        {
            return uploaded;
        }

        await WriteCacheAsync(CachedPhoto.From(uploaded.Value.PhotoId, "image/jpeg", jpeg), cancellationToken);
        await session.RefreshProfileAsync(cancellationToken);
        Changed?.Invoke(this, EventArgs.Empty);

        return ApiResult.Success();
    }

    public async Task<ApiResult> RemoveAsync(CancellationToken cancellationToken = default)
    {
        ApiResult removed = await users.DeletePhotoAsync(cancellationToken);
        if (!removed.IsSuccess)
        {
            return removed;
        }

        await db.SetValueAsync(CacheKey, string.Empty, cancellationToken);
        await session.RefreshProfileAsync(cancellationToken);
        Changed?.Invoke(this, EventArgs.Empty);

        return ApiResult.Success();
    }

    private async Task<CachedPhoto?> ReadCacheAsync(CancellationToken cancellationToken)
    {
        string? json = await db.GetValueAsync(CacheKey, cancellationToken);

        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<CachedPhoto>(json, ApiClient.JsonOptions);
    }

    private Task WriteCacheAsync(CachedPhoto photo, CancellationToken cancellationToken) =>
        db.SetValueAsync(CacheKey, JsonSerializer.Serialize(photo, ApiClient.JsonOptions), cancellationToken);

    internal sealed record CachedPhoto(Guid PhotoId, string DataUrl)
    {
        public static CachedPhoto From(Guid photoId, string contentType, byte[] bytes) =>
            new(photoId, $"data:{contentType};base64,{Convert.ToBase64String(bytes)}");
    }
}
