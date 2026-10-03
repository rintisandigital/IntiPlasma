using System.Globalization;
using Domain.Access;
using Microsoft.AspNetCore.Authorization;

namespace Web.App.Infrastructure.Authorization;

/// <summary>
/// Requires a right on a Web.App menu (W-2). On a controller and an action both apply, so an action usually
/// adds Create/Edit/Delete/Export on top of the controller's View. Every controller action must carry this
/// attribute, <see cref="AuthenticatedOnlyAttribute"/> or <c>[AllowAnonymous]</c> (architecture test).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class MenuAccessAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "menu:";

    public MenuAccessAttribute(string code, MenuRights right = MenuRights.View)
    {
        Code = code;
        Right = right;
        Policy = PolicyName(code, right);
    }

    public string Code { get; }

    public MenuRights Right { get; }

    public static string PolicyName(string code, MenuRights right) =>
        string.Create(CultureInfo.InvariantCulture, $"{PolicyPrefix}{code}:{right}");
}

/// <summary>
/// Pages every signed-in user may open (mainboard, dashboard, own account). The global authorize filter
/// already requires authentication; the marker documents the intent for the architecture test.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class AuthenticatedOnlyAttribute : Attribute;
