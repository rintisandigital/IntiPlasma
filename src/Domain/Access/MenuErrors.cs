using SharedKernel;

namespace Domain.Access;

public static class MenuErrors
{
    public static Error NotFound(Guid menuId) => Error.NotFound(
        "Menus.NotFound",
        $"The menu with the Id = '{menuId}' was not found");

    public static readonly Error NameRequired = Error.Problem(
        "Menus.NameRequired",
        "The menu name is required");
}
