using NetArchTest.Rules;
using Shouldly;

namespace ArchitectureTests.Layers;

/// <summary>
/// Web.App runs the use cases in-process (PLAN-WEBAPP W-1): it may reference Infrastructure for composition,
/// but its controllers stay thin and talk to the Application layer only.
/// </summary>
public sealed class WebAppTests : BaseTest
{
    private static readonly string WebApp = WebAppAssembly.GetName().Name!;

    [Fact]
    public void InnerLayers_Should_NotDependOn_WebApp()
    {
        TestResult result = Types.InAssemblies([DomainAssembly, ApplicationAssembly, InfrastructureAssembly])
            .Should()
            .NotHaveDependencyOn(WebApp)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void WebApp_Should_NotDependOn_WebApi()
    {
        TestResult result = Types.InAssembly(WebAppAssembly)
            .Should()
            .NotHaveDependencyOn(PresentationAssembly.GetName().Name)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void WebAppControllers_Should_NotUse_DataAccess_Directly()
    {
        TestResult result = Types.InAssembly(WebAppAssembly)
            .That()
            .ResideInNamespace("Web.App.Controllers")
            .Should()
            .NotHaveDependencyOnAny(
                "Infrastructure.Database",
                "Application.Abstractions.Data",
                "Microsoft.EntityFrameworkCore",
                "Dapper",
                "Npgsql")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }
}
