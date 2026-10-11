using Application.BreedStandards;
using Application.Production;
using Application.UnitTests.Abstractions;
using Domain.MasterData.BreedStandards;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.BreedStandards;

/// <summary>
/// Master Breed Standard (PLAN-MOBILE M-69 s.d. M-76).
/// </summary>
public sealed class BreedStandardHandlersTests : BaseHandlerTest
{
    private static readonly StandardPoint[] Rows =
    [
        new(0, 42m, null, 0m, null, 0m),
        new(7, 190m, 0.9m, 1m, null, 160m),
    ];

    [Fact]
    public async Task Create_Should_MakeTheFirstStandardDefault_AndRejectADuplicateCode()
    {
        using TestDbContext context = CreateDbContext();
        var handler = new CreateBreedStandardCommandHandler(context);

        Guid first = (await handler.Handle(new CreateBreedStandardCommand("cp707", "CP 707", "CP 707", null, Rows), default)).Value;
        Guid second = (await handler.Handle(new CreateBreedStandardCommand("COBB", "Cobb 500", null, null, Rows), default)).Value;
        Result<Guid> duplicate = await handler.Handle(new CreateBreedStandardCommand("CP707", "Lagi", null, null, Rows), default);

        duplicate.Error.ShouldBe(BreedStandardErrors.CodeNotUnique("CP707"));
        (await context.BreedStandards.SingleAsync(s => s.Id == first)).IsDefault.ShouldBeTrue();
        (await context.BreedStandards.SingleAsync(s => s.Id == second)).IsDefault.ShouldBeFalse();
    }

    [Fact]
    public async Task SetDefault_Should_MoveTheDefault()
    {
        using TestDbContext context = CreateDbContext();
        var create = new CreateBreedStandardCommandHandler(context);
        Guid first = (await create.Handle(new CreateBreedStandardCommand("A", "A", null, null, Rows), default)).Value;
        Guid second = (await create.Handle(new CreateBreedStandardCommand("B", "B", null, null, Rows), default)).Value;

        (await new SetDefaultBreedStandardCommandHandler(context).Handle(new SetDefaultBreedStandardCommand(second), default))
            .IsSuccess.ShouldBeTrue();

        (await context.BreedStandards.Where(s => s.IsDefault).Select(s => s.Id).ToListAsync()).ShouldBe([second]);

        // The old default can now be deactivated.
        (await new UpdateBreedStandardCommandHandler(context).Handle(
            new UpdateBreedStandardCommand(first, "A", null, null, IsActive: false, Rows), default)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Import_Should_ReplaceTheRows()
    {
        using TestDbContext context = CreateDbContext();
        Guid id = (await new CreateBreedStandardCommandHandler(context)
            .Handle(new CreateBreedStandardCommand("A", "A", null, null, Rows), default)).Value;

        Result<int> imported = await new ImportBreedStandardRowsCommandHandler(context).Handle(
            new ImportBreedStandardRowsCommand(id, "age;bw;fcr\n0;42;\n7;185,5;0,88\n14;470;1,02\n"), default);

        imported.Value.ShouldBe(3);
        BreedStandard standard = await context.BreedStandards.Include(s => s.Rows).SingleAsync(s => s.Id == id);
        standard.Rows.Select(r => r.BodyWeightGram).ShouldBe([42m, 185.5m, 470m]);
        standard.Rows.Last().Fcr.ShouldBe(1.02m);
    }

    [Fact]
    public void Csv_Should_ReportBadLines_AndRoundTrip()
    {
        BreedStandardCsv.Parse("bw,fcr\n42,1").Error.Code.ShouldBe("BreedStandards.InvalidCsv");

        Result<List<StandardPoint>> bad = BreedStandardCsv.Parse("age,bw\n0,42\n7,abc\n7.5,190\n");
        bad.Error.Description.ShouldContain("line 3");
        bad.Error.Description.ShouldContain("line 4");

        List<StandardPoint> parsed = BreedStandardCsv.Parse(BreedStandardCsv.Write(Rows)).Value;
        parsed.ShouldBe(Rows);
    }

    [Fact]
    public void Annotate_Should_JudgeBodyWeight_OnlyOnWeighingDays()
    {
        BreedStandardSnapshot standard = new(Guid.NewGuid(), "A", "A", null,
            [new StandardPoint(1, 50m, 1m, 0m, null, 10m), new StandardPoint(3, 90m, 1m, 1m, null, 50m)]);
        DateOnly chickIn = new(2026, 10, 1);

        List<DailyPerformance> days = StandardComparer.Annotate(
            CyclePerformanceBuilder.BuildDays(
                1_000,
                [new RecordedDay(chickIn.AddDays(1), 1, 5, 0, 10m, 47.5m), new RecordedDay(chickIn.AddDays(3), 3, 10, 0, 40m, null)],
                []),
            standard,
            1_000);

        days[0].Deviation!.BodyWeightPercent.ShouldBe(-5m);
        days[0].Deviation!.CumulativeFeedPercent.ShouldBe(0m);
        days[1].Standard!.BodyWeightGram.ShouldBe(90m);
        days[1].Deviation!.BodyWeightPercent.ShouldBeNull(); // 47,5 g carried forward is not compared with 90 g
        days[1].Deviation!.Fcr.ShouldBeNull();
        days[1].Deviation!.DepletionPoints.ShouldBe(0.5m); // 1,5% vs 1%

        StandardStatus status = StandardComparer.Latest(days, standard, 1_000, StandardThresholds.Default)!;
        status.WeighingAgeDays.ShouldBe(1);
        status.AgeDays.ShouldBe(3);
        status.BodyWeightBelow.ShouldBeFalse(); // exactly −5% is not beyond the threshold
        status.BelowStandard.ShouldBeFalse();

        StandardComparer.Latest(days, null, 1_000, StandardThresholds.Default).ShouldBeNull();
    }

    [Fact]
    public async Task FindEffective_Should_FallBackToTheDefault()
    {
        using TestDbContext context = CreateDbContext();
        Guid id = (await new CreateBreedStandardCommandHandler(context)
            .Handle(new CreateBreedStandardCommand("A", "A", null, null, Rows), default)).Value;

        BreedStandardSnapshot? effective = await BreedStandardReader.FindEffectiveAsync(context, null, default);

        effective!.Id.ShouldBe(id);
        effective.Rows.Count.ShouldBe(2);
        (await BreedStandardReader.FindEffectiveAsync(context, Guid.NewGuid(), default)).ShouldBeNull();
    }
}
