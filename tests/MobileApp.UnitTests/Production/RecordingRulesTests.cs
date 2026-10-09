using MobileApp.Core.Contracts;
using MobileApp.Core.Production;

namespace MobileApp.UnitTests.Production;

/// <summary>
/// Checks of a daily recording made on the device before it enters the queue (PLAN-MOBILE §3.6 step 2).
/// </summary>
public sealed class RecordingRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 9);

    [Fact]
    public void ValidDraft_Should_PassWithoutWarnings()
    {
        RecordingCheck check = RecordingRules.Check(Sample.Context(), Sample.Draft(), [], Today);

        check.IsValid.ShouldBeTrue();
        check.Warnings.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(10, "Tanggal tidak boleh melewati hari ini.")]
    [InlineData(-9, "Tanggal tidak boleh sebelum chick-in (01/10/2026).")]
    [InlineData(-1, "Recording tanggal ini sudah ada di server.")]
    public void Date_Should_BeTodayOrEarlier_AfterChickIn_AndNotRecordedYet(int days, string message)
    {
        RecordingDraft draft = Sample.Draft() with { Date = Today.AddDays(days) };

        RecordingRules.Check(Sample.Context(), draft, [], Today).ErrorFor(RecordingRules.Date).ShouldBe(message);
    }

    [Fact]
    public void Date_Should_NotRepeatAnEntryWaitingInTheQueue()
    {
        RecordingDraft queued = Sample.Draft();

        RecordingCheck check = RecordingRules.Check(Sample.Context(), Sample.Draft(), [queued], Today);

        check.ErrorFor(RecordingRules.Date).ShouldBe("Recording tanggal ini sudah ada di antrean (belum terkirim).");
    }

    [Fact]
    public void Editing_Should_NotCollideWithItself()
    {
        RecordingDraft queued = Sample.Draft();

        RecordingRules.Check(Sample.Context(), queued with { Mortality = 3 }, [queued], Today).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Depletion_Should_NotExceedThePopulationLeftByQueuedEntries()
    {
        // 1,000 birds on the server, 990 already reported dead in a queued entry of yesterday.
        RecordingDraft queued = Sample.Draft() with { Date = Today.AddDays(-2), Mortality = 990, Culling = 0, Usages = [] };
        RecordingDraft draft = Sample.Draft() with { Mortality = 8, Culling = 3 };

        RecordingCheck check = RecordingRules.Check(Sample.Context(), draft, [queued], Today);

        check.ErrorFor(RecordingRules.Mortality).ShouldBe("Mati + culling melebihi populasi berjalan (10 ekor).");
    }

    [Fact]
    public void UsageAboveTheCachedStock_Should_OnlyWarn()
    {
        // 100 kg in stock, 1 SAK (50 kg) queued, 2 SAK (100 kg) now.
        RecordingDraft queued = Sample.Draft() with { Date = Today.AddDays(-2), Usages = [new UsageInput(Sample.Feed, Sample.Sak, 1)] };
        RecordingDraft draft = Sample.Draft() with { Usages = [new UsageInput(Sample.Feed, Sample.Sak, 2)] };

        RecordingCheck check = RecordingRules.Check(Sample.Context(), draft, [queued], Today);

        check.IsValid.ShouldBeTrue();
        check.Warnings.ShouldHaveSingleItem().ShouldStartWith("Stok Pakan Starter di gudang kandang tercatat 50,00 KG");
    }

    [Fact]
    public void Usage_Should_UseAKnownUnitAndEachItemOnce()
    {
        RecordingDraft unknownUnit = Sample.Draft() with { Usages = [new UsageInput(Sample.Feed, Guid.NewGuid(), 1)] };
        RecordingDraft twice = Sample.Draft() with
        {
            Usages = [new UsageInput(Sample.Feed, Sample.Kg, 1), new UsageInput(Sample.Feed, Sample.Sak, 1)]
        };

        RecordingRules.Check(Sample.Context(), unknownUnit, [], Today).ErrorFor(RecordingRules.Usages)
            .ShouldBe("Lengkapi item, satuan, dan jumlah pemakaian (lebih dari 0).");
        RecordingRules.Check(Sample.Context(), twice, [], Today).ErrorFor(RecordingRules.Usages)
            .ShouldBe("Setiap item hanya boleh dicatat sekali.");
    }

    [Fact]
    public void Usage_Should_NeedACoopWarehouse()
    {
        FieldContext context = Sample.Context() with { Cycles = [Sample.Cycle() with { WarehouseId = null }] };

        RecordingRules.Check(context, Sample.Draft(), [], Today).ErrorFor(RecordingRules.Usages)
            .ShouldBe("Kandang ini belum memiliki gudang kandang; pemakaian tidak bisa dicatat.");
    }

    [Fact]
    public void UnknownCycle_Should_BeRejected() =>
        RecordingRules.Check(Sample.Context(), Sample.Draft() with { CycleId = Guid.NewGuid() }, [], Today)
            .ErrorFor(RecordingRules.Cycle).ShouldNotBeNull();

    [Theory]
    [InlineData(1.85, 10, 185.0)]
    [InlineData(0, 10, null)]
    [InlineData(2, 0, null)]
    public void AverageGram_Should_DivideTheSampleWeightByTheBirds(double kg, int birds, double? grams) =>
        RecordingRules.AverageGram((decimal)kg, birds).ShouldBe(grams is null ? null : (decimal)grams);

    [Fact]
    public void FeedKg_Should_ConvertToKilograms_AndSkipOvk()
    {
        RecordingDraft draft = Sample.Draft() with
        {
            Usages = [new UsageInput(Sample.Feed, Sample.Sak, 1.5m), new UsageInput(Sample.Ovk, Sample.Kg, 2)]
        };

        draft.FeedKg(Sample.Context()).ShouldBe(75m);
    }
}

/// <summary>
/// A field context with one cycle of 1,000 birds (chick-in 01/10/2026, recorded up to 08/10/2026) and 100 kg of
/// feed in the coop warehouse.
/// </summary>
internal static class Sample
{
    public static readonly Guid CycleId = Guid.Parse("0199c3a0-0000-7000-8000-000000000001");
    public static readonly Guid Warehouse = Guid.Parse("0199c3a0-0000-7000-8000-000000000002");
    public static readonly Guid Feed = Guid.Parse("0199c3a0-0000-7000-8000-000000000003");
    public static readonly Guid Ovk = Guid.Parse("0199c3a0-0000-7000-8000-000000000004");
    public static readonly Guid Kg = Guid.Parse("0199c3a0-0000-7000-8000-000000000005");
    public static readonly Guid Sak = Guid.Parse("0199c3a0-0000-7000-8000-000000000006");

    public static FieldCycle Cycle() => new()
    {
        Id = CycleId,
        Number = "SKL/BDG/2026/X/0001",
        Status = "Active",
        CoopName = "Kandang Sukamaju",
        WarehouseId = Warehouse,
        ChickInDate = new DateOnly(2026, 10, 1),
        InitialPopulation = 1_000,
        CurrentPopulation = 1_000,
        LastRecordingDate = new DateOnly(2026, 10, 8),
        RecordedDates = [new DateOnly(2026, 10, 7), new DateOnly(2026, 10, 8)]
    };

    public static FieldContext Context() => new()
    {
        ServerDate = new DateOnly(2026, 10, 9),
        Cycles = [Cycle()],
        Items =
        [
            new FieldItem(Feed, "PKN-01", "Pakan Starter", "Feed", Kg, "KG", [new FieldUom(Kg, "KG", 1), new FieldUom(Sak, "SAK", 50)]),
            new FieldItem(Ovk, "OVK-01", "Vitamin", "Ovk", Kg, "KG", [new FieldUom(Kg, "KG", 1)])
        ],
        Stock = [new FieldStock(Warehouse, Feed, 100)]
    };

    public static RecordingDraft Draft() => new()
    {
        CycleId = CycleId,
        CycleNumber = "SKL/BDG/2026/X/0001",
        CoopName = "Kandang Sukamaju",
        Date = new DateOnly(2026, 10, 9),
        Mortality = 2,
        Culling = 1,
        AverageBodyWeightGram = 180,
        Usages = [new UsageInput(Feed, Sak, 1)]
    };
}
