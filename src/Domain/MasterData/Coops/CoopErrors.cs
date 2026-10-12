using Domain.Common;
using SharedKernel;

namespace Domain.MasterData.Coops;

public static class CoopErrors
{
    public static Error NotFound(Guid coopId) => Error.NotFound(
        "Coops.NotFound",
        $"The coop with the Id = '{coopId}' was not found");

    public static Error CodeNotUnique(string code) => CommonErrors.CodeNotUnique("Coop", code);

    public static Error Inactive(Guid coopId) => CommonErrors.Inactive("Coop", coopId);

    public static Error PendingApproval(Guid coopId) => CommonErrors.PendingApproval("Coop", coopId);

    public static Error NotSubmittable(Guid coopId) => CommonErrors.NotSubmittable("Coop", coopId);

    public static Error NotPendingApproval(Guid coopId) => CommonErrors.NotPendingApproval("Coop", coopId);

    public static readonly Error FarmerNotSubmitted = Error.Problem(
        "Coops.FarmerNotSubmitted",
        "A coop can only be submitted for a farmer that is approved or waiting for approval");

    public static readonly Error FarmerNotApproved = Error.Problem(
        "Coops.FarmerNotApproved",
        "The coop can only be approved after its farmer has been approved");

    public static readonly Error InvalidCapacity = Error.Problem(
        "Coops.InvalidCapacity",
        "The capacity must be greater than zero");

    public static readonly Error InvalidLocation = Error.Problem(
        "Coops.InvalidLocation",
        "The latitude must be between -90 and 90 and the longitude between -180 and 180");
}
