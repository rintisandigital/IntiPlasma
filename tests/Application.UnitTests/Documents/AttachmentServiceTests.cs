using Application.Abstractions.Authorization;
using Application.Documents;
using Application.UnitTests.Abstractions;
using Domain.Common;
using Domain.Documents.Attachments;
using Domain.MasterData.Branches;
using Domain.MasterData.Farmers;
using Domain.Sales.SalesOrders;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Documents;

public sealed class AttachmentServiceTests : BaseHandlerTest
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x02, 0x03];
    private static readonly byte[] Pdf = [.. "%PDF-1.7\n%test"u8];

    private static Task<Result<AttachmentResponse>> UploadAsync(IAttachmentService service, byte[] content, string name = "foto.jpg", Guid? id = null) =>
        service.SaveAsync(new AttachmentUpload(new MemoryStream(content), name, id, null));

    // Copies of the empty value objects: the in-memory provider maps them as owned types, which cannot be shared.
    private static async Task<Farmer> SeedFarmerAsync(TestDbContext context, Guid? branchId = null)
    {
        Farmer farmer = Farmer.Create(
            $"P{Guid.NewGuid():N}"[..10], "Pak Budi", FarmerType.Inti, branchId ?? Guid.NewGuid(), null, TaxIdentity.None with { }, null, null, BankAccount.None with { }).Value;
        context.Farmers.Add(farmer);
        await context.SaveChangesAsync();

        return farmer;
    }

    [Fact]
    public async Task Save_Should_DetectTheTypeFromTheContent_AndStoreTheFile()
    {
        await using TestDbContext context = CreateDbContext();
        var storage = new InMemoryFileStorage();
        IAttachmentService service = CreateAttachments(context, storage: storage);

        AttachmentResponse photo = (await UploadAsync(service, Jpeg, @"C:\fakepath\kandang.png")).Value;
        AttachmentResponse document = (await UploadAsync(service, Pdf, "surat jalan.pdf")).Value;

        photo.ContentType.ShouldBe("image/jpeg");
        photo.Extension.ShouldBe(".jpg");
        photo.Kind.ShouldBe(nameof(AttachmentKind.Photo));
        photo.FileName.ShouldBe("kandang.png");
        photo.SizeBytes.ShouldBe(Jpeg.Length);
        photo.Status.ShouldBe(nameof(AttachmentStatus.Temporary));
        document.ContentType.ShouldBe("application/pdf");

        Attachment stored = await context.Attachments.SingleAsync(a => a.Id == photo.Id);
        storage.Files[stored.StoragePath].ShouldBe(Jpeg);
    }

    [Fact]
    public async Task Save_Should_RejectUnsupportedEmptyAndTooLargeFiles()
    {
        await using TestDbContext context = CreateDbContext();
        IAttachmentService service = CreateAttachments(context);

        (await UploadAsync(service, [.. "MZ executable"u8], "virus.jpg")).Error.ShouldBe(AttachmentErrors.UnsupportedType);
        (await UploadAsync(service, [])).Error.ShouldBe(AttachmentErrors.Empty);

        byte[] large = new byte[AttachmentFileTypes.MaxSizeBytes + 1];
        Jpeg.CopyTo(large, 0);
        (await UploadAsync(service, large)).Error.ShouldBe(AttachmentErrors.TooLarge(AttachmentFileTypes.MaxSizeBytes));

        (await context.Attachments.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Save_Should_BeIdempotent_ForTheSameIdAndContent()
    {
        await using TestDbContext context = CreateDbContext();
        IAttachmentService service = CreateAttachments(context);
        var id = Guid.CreateVersion7();

        AttachmentResponse first = (await UploadAsync(service, Jpeg, id: id)).Value;
        AttachmentResponse retry = (await UploadAsync(service, Jpeg, id: id)).Value;
        Result<AttachmentResponse> other = await UploadAsync(service, Pdf, id: id);

        retry.Id.ShouldBe(first.Id);
        other.Error.ShouldBe(AttachmentErrors.IdConflict(id));
        (await context.Attachments.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task SyncLinks_Should_LinkAndRelease_AndDeleteOnlyUnusedAttachments()
    {
        await using TestDbContext context = CreateDbContext();
        var storage = new InMemoryFileStorage();
        IAttachmentService service = CreateAttachments(context, storage: storage);
        Farmer farmer = await SeedFarmerAsync(context);
        Guid ktp = (await UploadAsync(service, Jpeg)).Value.Id;
        var owner = AttachmentOwner.Of(AttachmentOwnerTypes.Farmer, farmer.Id);

        (await service.ApplyDocumentsAsync(farmer, owner, farmer.BranchId, [ktp], CancellationToken.None)).IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync();

        Attachment attachment = await context.Attachments.SingleAsync(a => a.Id == ktp);
        attachment.Status.ShouldBe(AttachmentStatus.Linked);
        attachment.BranchId.ShouldBe(farmer.BranchId);
        farmer.Documents.ShouldBe([ktp]);
        (await service.DeleteAsync(ktp)).Error.ShouldBe(AttachmentErrors.InUse(ktp));

        // null keeps the list (update without documents), [] removes every attachment.
        (await service.ApplyDocumentsAsync(farmer, owner, farmer.BranchId, null, CancellationToken.None)).IsSuccess.ShouldBeTrue();
        farmer.Documents.ShouldBe([ktp]);
        (await service.ApplyDocumentsAsync(farmer, owner, farmer.BranchId, [], CancellationToken.None)).IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync();

        attachment.Status.ShouldBe(AttachmentStatus.Temporary);
        attachment.UnlinkedAtUtc.ShouldNotBeNull();
        (await context.AttachmentLinks.CountAsync()).ShouldBe(0);

        (await service.DeleteAsync(ktp)).IsSuccess.ShouldBeTrue();
        storage.Files.ShouldBeEmpty();
    }

    [Fact]
    public async Task SyncLinks_Should_KeepAnAttachmentLinked_WhileAnotherOwnerStillUsesIt()
    {
        await using TestDbContext context = CreateDbContext();
        IAttachmentService service = CreateAttachments(context);
        var branch = Guid.NewGuid();
        Farmer first = await SeedFarmerAsync(context, branch);
        Farmer second = await SeedFarmerAsync(context, branch);
        Guid shared = (await UploadAsync(service, Pdf, "berita acara.pdf")).Value.Id;
        var firstOwner = AttachmentOwner.Of(AttachmentOwnerTypes.Farmer, first.Id);
        var secondOwner = AttachmentOwner.Of(AttachmentOwnerTypes.Farmer, second.Id);

        // Same unit of work, like a feed mutation (stock return + stock transfer).
        await service.ApplyDocumentsAsync(first, firstOwner, branch, [shared], CancellationToken.None);
        await service.ApplyDocumentsAsync(second, secondOwner, branch, [shared], CancellationToken.None);
        await context.SaveChangesAsync();
        (await context.AttachmentLinks.CountAsync(l => l.AttachmentId == shared)).ShouldBe(2);

        await service.ApplyDocumentsAsync(first, firstOwner, branch, [], CancellationToken.None);
        await context.SaveChangesAsync();

        (await context.Attachments.SingleAsync(a => a.Id == shared)).Status.ShouldBe(AttachmentStatus.Linked);
    }

    [Fact]
    public async Task SyncLinks_Should_RejectUnknownForeignAndOtherBranchAttachments()
    {
        await using TestDbContext context = CreateDbContext();
        var branch = Guid.NewGuid();
        var uploader = Guid.NewGuid();
        IAttachmentService uploaderService = CreateAttachments(context, userId: uploader);
        Farmer farmer = await SeedFarmerAsync(context, branch);
        var owner = AttachmentOwner.Of(AttachmentOwnerTypes.Farmer, farmer.Id);
        Guid photo = (await UploadAsync(uploaderService, Jpeg)).Value.Id;
        var unknown = Guid.NewGuid();

        (await uploaderService.SyncLinksAsync(owner, branch, [unknown])).Error.ShouldBe(AttachmentErrors.Unknown(unknown));

        // A temporary attachment is private to its uploader (unless the user sees every branch).
        IBranchAccess branchUser = Substitute.For<IBranchAccess>();
        branchUser.GetScopeAsync(Arg.Any<CancellationToken>()).Returns(new BranchScope(false, [branch]));
        IAttachmentService otherUser = CreateAttachments(context, branchUser, Guid.NewGuid());
        (await otherUser.SyncLinksAsync(owner, branch, [photo])).Error.ShouldBe(AttachmentErrors.Unknown(photo));
        (await otherUser.EnsureAttachableAsync(branch, [photo])).Error.ShouldBe(AttachmentErrors.Unknown(photo));

        // Once linked to a branch it cannot be attached to a document of another branch.
        (await uploaderService.SyncLinksAsync(owner, branch, [photo])).IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync();
        var otherBranch = Guid.NewGuid();
        (await uploaderService.EnsureAttachableAsync(otherBranch, [photo])).Error.ShouldBe(AttachmentErrors.BranchMismatch(photo));
        (await uploaderService.SyncLinksAsync(AttachmentOwner.Of(AttachmentOwnerTypes.Coop, Guid.NewGuid()), otherBranch, [photo]))
            .Error.ShouldBe(AttachmentErrors.BranchMismatch(photo));
    }

    [Fact]
    public async Task SetDocuments_Should_ReplaceTheAttachments_ButNotOnCancelledDocuments()
    {
        await using TestDbContext context = CreateDbContext();
        IAttachmentService service = CreateAttachments(context);
        var branch = Branch.Create("BDG", "Bandung", null, null);
        SalesOrder order = SalesOrder.Create("SO/1", branch.Id, Guid.NewGuid(), new DateOnly(2026, 10, 1), null, null,
            [new SalesOrderLineInput(Guid.NewGuid(), 100, 200m, new Money(20_000m), null)]).Value;
        context.SalesOrders.Add(order);
        await context.SaveChangesAsync();
        Guid contract = (await UploadAsync(service, Pdf, "kontrak.pdf")).Value.Id;

        IBranchAccess allBranches = Substitute.For<IBranchAccess>();
        allBranches.GetScopeAsync(Arg.Any<CancellationToken>()).Returns(BranchScope.All);
        var handler = new SetDocumentsCommandHandler(context, allBranches, NoFieldScope(), service);

        (await handler.Handle(new SetDocumentsCommand(AttachmentOwnerTypes.SalesOrder, order.Id, [contract]), CancellationToken.None))
            .IsSuccess.ShouldBeTrue();
        (await context.SalesOrders.SingleAsync(o => o.Id == order.Id)).Documents.ShouldBe([contract]);

        order.Cancel("Batal");
        await context.SaveChangesAsync();

        (await handler.Handle(new SetDocumentsCommand(AttachmentOwnerTypes.SalesOrder, order.Id, []), CancellationToken.None))
            .Error.ShouldBe(DocumentErrors.OwnerCancelled);
        (await handler.Handle(new SetDocumentsCommand(AttachmentOwnerTypes.SalesOrder, Guid.NewGuid(), []), CancellationToken.None))
            .Error.Type.ShouldBe(ErrorType.NotFound);
    }
}
