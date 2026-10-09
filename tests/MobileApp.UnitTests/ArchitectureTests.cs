using System.Reflection;
using MobileApp.Core.Api;

namespace MobileApp.UnitTests;

public sealed class ArchitectureTests
{
    [Fact]
    public void Core_Should_NotReferenceTheServerProjects()
    {
        // The app talks to Web.Api over HTTP only (PLAN-MOBILE M-9, §7.2).
        string[] forbidden = ["Domain", "Application", "Infrastructure", "SharedKernel", "Web.Api", "Web.App"];

        AssemblyName[] references = typeof(ApiClient).Assembly.GetReferencedAssemblies();

        references.Select(r => r.Name).Where(n => forbidden.Contains(n)).ShouldBeEmpty();
    }
}
