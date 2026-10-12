using Domain.MasterData.Coops;
using Domain.MasterData.Farmers;

namespace Application.UnitTests.Abstractions;

/// <summary>
/// Farmers and coops go through the approval module (PLAN-MOBILE M-81); tests that just need active ones approve
/// them directly.
/// </summary>
internal static class ApprovedEntities
{
    public static Farmer Approved(this Farmer farmer)
    {
        farmer.Submit();
        farmer.Approve();

        return farmer;
    }

    public static Coop Approved(this Coop coop, Farmer farmer)
    {
        coop.Submit(farmer);
        coop.Approve(farmer);

        return coop;
    }
}
