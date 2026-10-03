using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using Web.App.Infrastructure.Authorization;

namespace ArchitectureTests.Layers;

/// <summary>
/// Web.App permissions are only enforced by attributes (W-2): every action must declare who may run it.
/// </summary>
public sealed class WebAppAuthorizationTests : BaseTest
{
    private static IEnumerable<(Type Controller, MethodInfo Action)> Actions() =>
        WebAppAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsAssignableTo(typeof(ControllerBase)))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() is null)
                .Select(m => (t, m)));

    [Fact]
    public void EveryAction_Should_DeclareMenuAccess_AuthenticatedOnly_OrAllowAnonymous()
    {
        static bool Declares(MemberInfo member) =>
            member.IsDefined(typeof(MenuAccessAttribute), inherit: true) ||
            member.IsDefined(typeof(AuthenticatedOnlyAttribute), inherit: true) ||
            member.IsDefined(typeof(AllowAnonymousAttribute), inherit: true);

        var missing = Actions()
            .Where(a => !Declares(a.Action) && !Declares(a.Controller))
            .Select(a => $"{a.Controller.Name}.{a.Action.Name}")
            .ToList();

        missing.ShouldBeEmpty();
    }

    [Fact]
    public void EveryMenuAccessCode_Should_ExistInTheCatalog()
    {
        var unknown = Actions()
            .SelectMany(a => a.Action.GetCustomAttributes<MenuAccessAttribute>()
                .Concat(a.Controller.GetCustomAttributes<MenuAccessAttribute>()))
            .Select(a => a.Code)
            .Distinct()
            .Where(code => !MenuCatalog.Contains(code))
            .ToList();

        unknown.ShouldBeEmpty();
    }

    [Fact]
    public void MenuCatalog_Codes_Should_BeUnique_AndPagesBelongToAGroup()
    {
        var codes = MenuCatalog.Definitions.Select(d => d.Code).ToList();
        codes.Distinct(StringComparer.Ordinal).Count().ShouldBe(codes.Count);

        var groups = MenuCatalog.Definitions.Where(d => d.ParentCode is null).Select(d => d.Code).ToHashSet();
        MenuCatalog.Definitions
            .Where(d => d.ParentCode is not null)
            .ShouldAllBe(d => groups.Contains(d.ParentCode!) && d.Route != null);
    }
}
