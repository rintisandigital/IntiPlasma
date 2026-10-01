using System.Linq.Expressions;
using System.Security.Cryptography;
using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Storage;
using Domain.Common;
using Domain.Documents.Attachments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Documents;

internal sealed partial class AttachmentService(
    IApplicationDbContext context,
    IFileStorage fileStorage,
    IBranchAccess branchAccess,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<AttachmentService> logger) : IAttachmentService
{
    public async Task<Result<AttachmentResponse>> SaveAsync(AttachmentUpload upload, CancellationToken cancellationToken = default)
    {
        await using var buffer = new MemoryStream();
        await CopyLimitedAsync(upload.Content, buffer, AttachmentFileTypes.MaxSizeBytes + 1, cancellationToken);

        if (buffer.Length == 0)
        {
            return Result.Failure<AttachmentResponse>(AttachmentErrors.Empty);
        }

        if (buffer.Length > AttachmentFileTypes.MaxSizeBytes)
        {
            return Result.Failure<AttachmentResponse>(AttachmentErrors.TooLarge(AttachmentFileTypes.MaxSizeBytes));
        }

        byte[] bytes = buffer.ToArray();
        AttachmentFile? file = AttachmentFileTypes.Detect(bytes.AsSpan(0, Math.Min(bytes.Length, AttachmentFileTypes.HeaderLength)), bytes.Length);
        if (file is null)
        {
            return Result.Failure<AttachmentResponse>(AttachmentErrors.UnsupportedType);
        }

        string checksum = Convert.ToHexStringLower(SHA256.HashData(bytes));

        if (upload.Id is { } requestedId)
        {
            Attachment? existing = await context.Attachments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(a => a.Id == requestedId, cancellationToken);

            if (existing is not null)
            {
                return !existing.IsDeleted && existing.IsSameContent(checksum, bytes.Length) && await CanReadAsync(existing, cancellationToken)
                    ? ToResponse(existing)
                    : Result.Failure<AttachmentResponse>(AttachmentErrors.IdConflict(requestedId));
            }
        }

        Result<Attachment> attachment = Attachment.Create(
            upload.Id ?? Guid.CreateVersion7(),
            upload.FileName,
            file,
            checksum,
            DateOnly.FromDateTime(dateTimeProvider.UtcNow),
            upload.Description);

        if (attachment.IsFailure)
        {
            return Result.Failure<AttachmentResponse>(attachment.Error);
        }

        buffer.Position = 0;
        await fileStorage.SaveAsync(attachment.Value.StoragePath, buffer, cancellationToken);

        try
        {
            context.Attachments.Add(attachment.Value);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await fileStorage.DeleteAsync(attachment.Value.StoragePath, CancellationToken.None);
            throw;
        }

        return ToResponse(attachment.Value);
    }

    public async Task<Result<AttachmentResponse>> GetAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        Result<Attachment> attachment = await FindReadableAsync(attachmentId, tracking: false, cancellationToken);

        return attachment.IsSuccess ? ToResponse(attachment.Value) : Result.Failure<AttachmentResponse>(attachment.Error);
    }

    public async Task<IReadOnlyList<AttachmentResponse>> GetManyAsync(
        IReadOnlyCollection<Guid> attachmentIds,
        CancellationToken cancellationToken = default)
    {
        if (attachmentIds.Count == 0)
        {
            return [];
        }

        List<Attachment> attachments = await context.Attachments
            .AsNoTracking()
            .Where(a => attachmentIds.Contains(a.Id))
            .ToListAsync(cancellationToken);

        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        return [.. attachments.Where(a => CanRead(a, scope)).OrderBy(a => a.CreatedAtUtc).Select(ToResponse)];
    }

    public async Task<Result<AttachmentContent>> OpenAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        Result<Attachment> attachment = await FindReadableAsync(attachmentId, tracking: false, cancellationToken);
        if (attachment.IsFailure)
        {
            return Result.Failure<AttachmentContent>(attachment.Error);
        }

        Stream? content = await fileStorage.OpenReadAsync(attachment.Value.StoragePath, cancellationToken);
        if (content is null)
        {
            LogFileMissing(logger, attachment.Value.Id, attachment.Value.StoragePath);
            return Result.Failure<AttachmentContent>(AttachmentErrors.NotFound(attachmentId));
        }

        return new AttachmentContent(
            content,
            attachment.Value.FileName,
            attachment.Value.ContentType,
            attachment.Value.Kind == AttachmentKind.Photo);
    }

    public async Task<Result> DeleteAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        Result<Attachment> attachment = await FindReadableAsync(attachmentId, tracking: true, cancellationToken);
        if (attachment.IsFailure)
        {
            return attachment;
        }

        Result deletable = attachment.Value.EnsureDeletable();
        if (deletable.IsFailure)
        {
            return deletable;
        }

        context.Attachments.Remove(attachment.Value);
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            await fileStorage.DeleteAsync(attachment.Value.StoragePath, cancellationToken);
        }
        catch (IOException ex)
        {
            // The metadata is already deleted; an orphaned file only costs disk space.
            LogFileDeleteFailed(logger, ex, attachment.Value.Id, attachment.Value.StoragePath);
        }

        return Result.Success();
    }

    public async Task<Result> EnsureAttachableAsync(
        Guid? ownerBranchId,
        IReadOnlyCollection<Guid>? documents,
        CancellationToken cancellationToken = default)
    {
        if (documents is null || documents.Count == 0)
        {
            return Result.Success();
        }

        Result<Guid[]> normalized = DocumentList.Normalize(documents);
        if (normalized.IsFailure)
        {
            return normalized;
        }

        Guid[] ids = normalized.Value;
        List<Attachment> attachments = await context.Attachments
            .AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .ToListAsync(cancellationToken);

        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        foreach (Guid id in ids)
        {
            Attachment? attachment = attachments.Find(a => a.Id == id);
            if (attachment is null || !CanRead(attachment, scope))
            {
                return Result.Failure(AttachmentErrors.Unknown(id));
            }

            if (ownerBranchId is not null && attachment.BranchId is not null && attachment.BranchId != ownerBranchId)
            {
                return Result.Failure(AttachmentErrors.BranchMismatch(id));
            }
        }

        return Result.Success();
    }

    public async Task<Result> SyncLinksAsync(
        AttachmentOwner owner,
        Guid? ownerBranchId,
        IReadOnlyCollection<Guid> documents,
        CancellationToken cancellationToken = default)
    {
        List<AttachmentLink> current = await context.AttachmentLinks
            .Where(OwnedBy(owner))
            .ToListAsync(cancellationToken);

        var currentIds = current.Select(l => l.AttachmentId).ToHashSet();
        var addedIds = documents.Where(id => !currentIds.Contains(id)).Distinct().ToList();
        var removed = current.Where(l => !documents.Contains(l.AttachmentId)).ToList();

        if (addedIds.Count > 0)
        {
            Result linked = await LinkAsync(owner, ownerBranchId, addedIds, cancellationToken);
            if (linked.IsFailure)
            {
                return linked;
            }
        }

        if (removed.Count > 0)
        {
            await UnlinkAsync(owner, removed, cancellationToken);
        }

        return Result.Success();
    }

    private async Task<Result> LinkAsync(
        AttachmentOwner owner,
        Guid? ownerBranchId,
        List<Guid> attachmentIds,
        CancellationToken cancellationToken)
    {
        List<Attachment> attachments = await context.Attachments
            .Where(a => attachmentIds.Contains(a.Id))
            .ToListAsync(cancellationToken);

        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        foreach (Guid attachmentId in attachmentIds)
        {
            Attachment? attachment = attachments.Find(a => a.Id == attachmentId);
            if (attachment is null || !CanRead(attachment, scope))
            {
                return Result.Failure(AttachmentErrors.Unknown(attachmentId));
            }

            Result linked = attachment.Link(ownerBranchId);
            if (linked.IsFailure)
            {
                return linked;
            }

            context.AttachmentLinks.Add(new AttachmentLink(attachmentId, owner));
        }

        return Result.Success();
    }

    private async Task UnlinkAsync(AttachmentOwner owner, List<AttachmentLink> removed, CancellationToken cancellationToken)
    {
        context.AttachmentLinks.RemoveRange(removed);

        var removedIds = removed.Select(l => l.AttachmentId).ToList();
        Expression<Func<AttachmentLink, bool>> ownedBy = OwnedBy(owner);
        Func<AttachmentLink, bool> isOwner = ownedBy.Compile();

        // Still used by another owner, either saved already or added in this unit of work (feed mutation).
        var stillUsed = (await context.AttachmentLinks
                .Where(l => removedIds.Contains(l.AttachmentId))
                .Where(Not(ownedBy))
                .Select(l => l.AttachmentId)
                .ToListAsync(cancellationToken))
            .Concat(context.AttachmentLinks.Local.Where(l => removedIds.Contains(l.AttachmentId) && !isOwner(l)).Select(l => l.AttachmentId))
            .ToHashSet();

        var releasedIds = removedIds.Where(id => !stillUsed.Contains(id)).ToList();
        if (releasedIds.Count == 0)
        {
            return;
        }

        List<Attachment> released = await context.Attachments
            .Where(a => releasedIds.Contains(a.Id))
            .ToListAsync(cancellationToken);

        foreach (Attachment attachment in released)
        {
            attachment.Unlink(dateTimeProvider.UtcNow);
        }
    }

    private async Task<Result<Attachment>> FindReadableAsync(Guid attachmentId, bool tracking, CancellationToken cancellationToken)
    {
        IQueryable<Attachment> query = tracking ? context.Attachments : context.Attachments.AsNoTracking();

        Attachment? attachment = await query.SingleOrDefaultAsync(a => a.Id == attachmentId, cancellationToken);

        return attachment is not null && await CanReadAsync(attachment, cancellationToken)
            ? attachment
            : Result.Failure<Attachment>(AttachmentErrors.NotFound(attachmentId));
    }

    private async Task<bool> CanReadAsync(Attachment attachment, CancellationToken cancellationToken) =>
        CanRead(attachment, await branchAccess.GetScopeAsync(cancellationToken));

    /// <summary>
    /// A temporary attachment is private to its uploader (head office users see everything). Once linked it follows
    /// the branch of its owner; attachments of company-wide masters (vendor, customer) are visible to every user.
    /// </summary>
    private bool CanRead(Attachment attachment, BranchScope scope)
    {
        if (scope.AllBranches)
        {
            return true;
        }

        if (attachment.Status == AttachmentStatus.Temporary)
        {
            return attachment.CreatedBy == userContext.UserId;
        }

        return attachment.BranchId is not { } branchId || scope.CanAccess(branchId);
    }

    private static Expression<Func<AttachmentLink, bool>> OwnedBy(AttachmentOwner owner) =>
        l => l.OwnerType == owner.Type && l.OwnerId == owner.Id && l.OwnerKey == owner.Key;

    private static Expression<Func<AttachmentLink, bool>> Not(Expression<Func<AttachmentLink, bool>> predicate) =>
        Expression.Lambda<Func<AttachmentLink, bool>>(Expression.Not(predicate.Body), predicate.Parameters);

    private static async Task CopyLimitedAsync(Stream source, Stream destination, long limit, CancellationToken cancellationToken)
    {
        byte[] chunk = new byte[81920];
        long total = 0;
        int read;

        while (total < limit && (read = await source.ReadAsync(chunk.AsMemory(0, (int)Math.Min(chunk.Length, limit - total)), cancellationToken)) > 0)
        {
            await destination.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            total += read;
        }
    }

    private static AttachmentResponse ToResponse(Attachment attachment) => new()
    {
        Id = attachment.Id,
        FileName = attachment.FileName,
        ContentType = attachment.ContentType,
        Extension = attachment.Extension,
        SizeBytes = attachment.SizeBytes,
        Kind = attachment.Kind.ToString(),
        Checksum = attachment.Checksum,
        Description = attachment.Description,
        Status = attachment.Status.ToString(),
        BranchId = attachment.BranchId,
        CreatedAtUtc = attachment.CreatedAtUtc,
        CreatedBy = attachment.CreatedBy
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "File of attachment {AttachmentId} is missing at {StoragePath}")]
    private static partial void LogFileMissing(ILogger logger, Guid attachmentId, string storagePath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "File of deleted attachment {AttachmentId} could not be removed from {StoragePath}")]
    private static partial void LogFileDeleteFailed(ILogger logger, Exception exception, Guid attachmentId, string storagePath);
}
