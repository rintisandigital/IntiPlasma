using System.Reflection;
using NetArchTest.Rules;
using SharedKernel;
using Shouldly;

namespace ArchitectureTests.Domain;

public class DomainTests : BaseTest
{
    [Fact]
    public void DomainEvents_Should_BeSealedRecordsDerivedFromDomainEvent()
    {
        TestResult result = Types.InAssembly(DomainAssembly)
            .That()
            .ImplementInterface(typeof(IDomainEvent))
            .Should()
            .BeSealed()
            .And()
            .Inherit(typeof(DomainEvent))
            .GetResult();

        result.FailingTypeNames.ShouldBeNull();
    }

    [Fact]
    public void Entities_Should_NotExposePublicSetters()
    {
        var violations = DomainAssembly
            .GetTypes()
            .Where(t => t.IsAssignableTo(typeof(Entity)))
            .SelectMany(t => t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(p => p.SetMethod?.IsPublic == true)
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
            .ToList();

        violations.ShouldBeEmpty();
    }
}
