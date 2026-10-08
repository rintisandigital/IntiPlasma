using Domain.MasterData.Coops;
using FluentValidation;

namespace Application.Coops;

internal sealed class CoopProfileValidator : AbstractValidator<CoopProfile>
{
    private const int MaxRows = 50;

    public CoopProfileValidator()
    {
        RuleFor(p => p.Phone).MaximumLength(30);
        RuleFor(p => p.Country).MaximumLength(60);
        RuleFor(p => p.Province).MaximumLength(60);
        RuleFor(p => p.City).MaximumLength(100);
        RuleFor(p => p.LastPartner).MaximumLength(150);
        RuleFor(p => p.Construction).MaximumLength(60);
        RuleFor(p => p.Curtain).MaximumLength(60);
        RuleFor(p => p.GensetBrand).MaximumLength(60);
        RuleFor(p => p.LocationAccess).IsInEnum();

        RuleFor(p => p.CollectorNotes).MaximumLength(500);
        RuleFor(p => p.FarmerNotes).MaximumLength(500);
        RuleFor(p => p.OtherPartnerNotes).MaximumLength(500);
        RuleFor(p => p.OtherNotes).MaximumLength(500);
        RuleFor(p => p.Remarks).MaximumLength(1000);

        RuleFor(p => p.LengthM).GreaterThanOrEqualTo(0);
        RuleFor(p => p.WidthM).GreaterThanOrEqualTo(0);
        RuleFor(p => p.HeightM).GreaterThanOrEqualTo(0);
        RuleFor(p => p.Floors).GreaterThanOrEqualTo(1);
        RuleFor(p => p.NippleUnits).GreaterThanOrEqualTo(0);
        RuleFor(p => p.TmaoUnits).GreaterThanOrEqualTo(0);
        RuleFor(p => p.ChickFeederUnits).GreaterThanOrEqualTo(0);
        RuleFor(p => p.TraUnits).GreaterThanOrEqualTo(0);
        RuleFor(p => p.SuperFeederUnits).GreaterThanOrEqualTo(0);
        RuleFor(p => p.AugerUnits).GreaterThanOrEqualTo(0);
        RuleFor(p => p.GensetKva).GreaterThanOrEqualTo(0);
        RuleFor(p => p.TonnageCapacityKg).GreaterThanOrEqualTo(0);
        RuleFor(p => p.InitialDensityBirdsPerM2).GreaterThanOrEqualTo(0);
        RuleFor(p => p.InitialDensityKgPerM2).GreaterThanOrEqualTo(0);
        RuleFor(p => p.FinalDensityBirdsPerM2).GreaterThanOrEqualTo(0);
        RuleFor(p => p.FinalDensityKgPerM2).GreaterThanOrEqualTo(0);
        RuleFor(p => p.FeedStorageKg).GreaterThanOrEqualTo(0);
        RuleFor(p => p.HuskStorageSacks).GreaterThanOrEqualTo(0);
        RuleFor(p => p.Heaters).GreaterThanOrEqualTo(0);

        RuleFor(p => p.Fans).Must(rows => rows is null || rows.Length <= MaxRows);
        RuleForEach(p => p.Fans).ChildRules(fan =>
        {
            fan.RuleFor(f => f.Brand).MaximumLength(60);
            fan.RuleFor(f => f.Capacity).GreaterThanOrEqualTo(0);
            fan.RuleFor(f => f.Quantity).GreaterThanOrEqualTo(0);
        });

        RuleFor(p => p.FanTests).Must(rows => rows is null || rows.Length <= MaxRows);
        RuleForEach(p => p.FanTests).ChildRules(test =>
        {
            test.RuleFor(t => t.FrontMs).GreaterThanOrEqualTo(0);
            test.RuleFor(t => t.MiddleMs).GreaterThanOrEqualTo(0);
            test.RuleFor(t => t.BackMs).GreaterThanOrEqualTo(0);
        });

        RuleFor(p => p.Thinnings).Must(rows => rows is null || rows.Length <= MaxRows);
        RuleForEach(p => p.Thinnings).ChildRules(thinning =>
        {
            thinning.RuleFor(t => t.WeightKg).GreaterThanOrEqualTo(0);
            thinning.RuleFor(t => t.Birds).GreaterThanOrEqualTo(0);
        });
    }
}
