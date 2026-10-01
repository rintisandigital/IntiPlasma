using Domain.Common;
using Domain.Documents.Attachments;
using Domain.Finance.Journals;
using Domain.MasterData.Farmers;
using Domain.Partnership.Cycles;
using Domain.Sales.SalesOrders;
using SharedKernel;

namespace Domain.UnitTests.Documents;

public sealed class DocumentsTests
{
    private static readonly AttachmentFile Jpeg = new("image/jpeg", ".jpg", AttachmentKind.Photo, 1_024);
    private static readonly DateOnly Today = new(2026, 10, 2);

    private static Attachment NewAttachment() =>
        Attachment.Create(Guid.CreateVersion7(), "foto.jpg", Jpeg, new string('a', 64), Today, null).Value;

    [Theory]
    [InlineData(@"C:\Users\ppl\Pictures\kandang 1.jpg", "kandang 1.jpg")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("  surat\u0000jalan.pdf  ", "suratjalan.pdf")]
    [InlineData("", "file")]
    [InlineData(null, "file")]
    [InlineData("...", "file")]
    public void SanitizeFileName_Should_StripPathsAndControlCharacters(string? input, string expected)
    {
        Attachment.SanitizeFileName(input).ShouldBe(expected);
    }

    [Fact]
    public void SanitizeFileName_Should_KeepTheExtension_WhenShortened()
    {
        string name = Attachment.SanitizeFileName(new string('x', 300) + ".pdf");

        name.Length.ShouldBe(Attachment.FileNameMaxLength);
        name.ShouldEndWith(".pdf");
    }

    [Fact]
    public void Create_Should_StoreUnderTheIdAndUploadMonth()
    {
        var id = Guid.CreateVersion7();

        Attachment attachment = Attachment.Create(id, "Tiket Timbangan.JPG", Jpeg, new string('b', 64), Today, "  truk 1 ").Value;

        attachment.StoredFileName.ShouldBe($"{id:N}.jpg");
        attachment.StoragePath.ShouldBe($"2026/10/{id:N}.jpg");
        attachment.FileName.ShouldBe("Tiket Timbangan.JPG");
        attachment.Description.ShouldBe("truk 1");
        attachment.Status.ShouldBe(AttachmentStatus.Temporary);
        attachment.Kind.ShouldBe(AttachmentKind.Photo);
    }

    [Fact]
    public void Create_Should_Fail_ForEmptyFileOrId()
    {
        Attachment.Create(Guid.Empty, "a.jpg", Jpeg, "x", Today, null).Error.ShouldBe(AttachmentErrors.InvalidId);
        Attachment.Create(Guid.NewGuid(), "a.jpg", Jpeg with { SizeBytes = 0 }, "x", Today, null).Error.ShouldBe(AttachmentErrors.Empty);
    }

    [Fact]
    public void Link_Should_AdoptTheOwnerBranch_AndRejectAnotherBranch()
    {
        Attachment attachment = NewAttachment();
        var branch = Guid.NewGuid();

        attachment.Link(branch).IsSuccess.ShouldBeTrue();
        attachment.Status.ShouldBe(AttachmentStatus.Linked);
        attachment.BranchId.ShouldBe(branch);

        attachment.Link(Guid.NewGuid()).Error.ShouldBe(AttachmentErrors.BranchMismatch(attachment.Id));

        // Company-wide owners (vendor, customer) have no branch and accept any attachment.
        attachment.Link(null).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Delete_Should_BeRejected_WhileLinked_AndAllowedAfterUnlink()
    {
        Attachment attachment = NewAttachment();
        attachment.Link(null);

        attachment.EnsureDeletable().Error.ShouldBe(AttachmentErrors.InUse(attachment.Id));

        var now = new DateTime(2026, 10, 2, 8, 0, 0, DateTimeKind.Utc);
        attachment.Unlink(now);

        attachment.Status.ShouldBe(AttachmentStatus.Temporary);
        attachment.UnlinkedAtUtc.ShouldBe(now);
        attachment.EnsureDeletable().IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Normalize_Should_RemoveDuplicates_AndKeepTheOrder()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid();

        DocumentList.Normalize([b, a, b]).Value.ShouldBe([b, a]);
        DocumentList.Normalize(null).Value.ShouldBeEmpty();
    }

    [Fact]
    public void Normalize_Should_RejectEmptyIdsAndTooManyDocuments()
    {
        DocumentList.Normalize([Guid.Empty]).Error.ShouldBe(DocumentErrors.Invalid);
        DocumentList.Normalize(Enumerable.Range(0, DocumentList.MaxDocuments + 1).Select(_ => Guid.NewGuid()))
            .Error.ShouldBe(DocumentErrors.TooMany);
        DocumentList.Normalize(Enumerable.Range(0, DocumentList.MaxDocuments).Select(_ => Guid.NewGuid()))
            .IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void SetDocuments_Should_ReplaceTheList_AndBeRejectedOnceCancelled()
    {
        SalesOrder order = SalesOrder.Create("SO/1", Guid.NewGuid(), Guid.NewGuid(), Today, null, null,
            [new SalesOrderLineInput(Guid.NewGuid(), 100, 200m, new Money(20_000m), null)]).Value;
        Guid first = Guid.NewGuid(), second = Guid.NewGuid();

        order.SetDocuments([first, second]).IsSuccess.ShouldBeTrue();
        order.SetDocuments([second]).IsSuccess.ShouldBeTrue();
        order.Documents.ShouldBe([second]);

        order.Cancel("Batal");

        order.SetDocuments([first]).Error.ShouldBe(DocumentErrors.OwnerCancelled);
        order.Documents.ShouldBe([second]);
    }

    [Fact]
    public void SetDocuments_Should_OnlyBeAllowedOnManualJournals()
    {
        var cash = Guid.NewGuid();
        var revenue = Guid.NewGuid();
        JournalLineInput[] lines = [JournalLineInput.DebitLine(cash, new Money(100m)), JournalLineInput.CreditLine(revenue, new Money(100m))];

        JournalEntry manual = JournalEntry.CreateManual(Guid.NewGuid(), Today, "Manual", lines).Value;
        JournalEntry automatic = JournalEntry.CreateAutomatic(Guid.NewGuid(), Today, "Auto", "SalesInvoice", Guid.NewGuid(), lines).Value;

        manual.SetDocuments([Guid.NewGuid()]).IsSuccess.ShouldBeTrue();
        automatic.SetDocuments([Guid.NewGuid()]).Error.ShouldBe(DocumentErrors.NotAllowed);
    }

    [Fact]
    public void SetHarvestDocuments_Should_ChangeOnlyThatHarvest()
    {
        Farmer farmer = TestData.IntiFarmer();
        ProductionCycle cycle = ProductionCycle.Plan("SKL/1", TestData.Coop(farmer), farmer, null, Today, 1_000, null).Value;
        cycle.Start(Today, 1_000);
        CycleHarvest first = cycle.RecordHarvest(Today.AddDays(30), 400, 800m, null).Value;
        CycleHarvest second = cycle.RecordHarvest(Today.AddDays(31), 400, 820m, null).Value;
        var ticket = Guid.NewGuid();

        cycle.SetHarvestDocuments(second.Id, [ticket]).IsSuccess.ShouldBeTrue();

        second.Documents.ShouldBe([ticket]);
        first.Documents.ShouldBeEmpty();
        cycle.SetHarvestDocuments(Guid.NewGuid(), [ticket]).Error.Code.ShouldBe("Cycles.HarvestNotFound");
    }
}
