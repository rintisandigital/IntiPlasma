using System.Reflection;
using Domain.Roles;
using Domain.Users;
using MobileApp.Core.Api;
using MobileApp.Core.Session;
using SharedKernel;

namespace MobileApp.UnitTests;

/// <summary>
/// The app keeps its own copies of permission strings and error codes (it only talks HTTP to Web.Api); these tests
/// fail when the server catalogs change underneath them.
/// </summary>
public sealed class CatalogSyncTests
{
    /// <summary>
    /// Codes produced outside Domain/SharedKernel: by Web.Api's exception handler or by the client itself.
    /// </summary>
    private static readonly string[] NonDomainCodes =
    [
        "Concurrency.Conflict",
        "Database.UniqueViolation",
        ApiError.NetworkCode,
        ApiError.SessionExpiredCode,
        ApiError.UnexpectedCode
    ];

    [Fact]
    public void AppPermissions_Should_ExistInTheServerCatalog() =>
        AppPermissions.All.Where(p => !Permissions.Exists(p)).ShouldBeEmpty();

    [Fact]
    public void ErrorMessages_Should_OnlyMapExistingErrorCodes()
    {
        HashSet<string> serverCodes = ServerErrorCodes();

        ErrorMessages.Known.Keys
            .Where(code => !serverCodes.Contains(code) && !NonDomainCodes.Contains(code))
            .ShouldBeEmpty();
    }

    [Fact]
    public void LockedOut_Should_StillBeTheCodeWithMinutesInItsDescription()
    {
        Error lockedOut = UserErrors.LockedOut(12);

        ErrorMessages.For(lockedOut.Code, 400, lockedOut.Description)
            .ShouldBe("Akun terkunci karena terlalu banyak percobaan masuk yang gagal. Coba lagi dalam 12 menit.");
    }

    [Fact]
    public void For_Should_FallBackToTheStatusAndKeepTheServerDetail() =>
        ErrorMessages.For("Farmers.Unknown", 404, "Farmer was not found")
            .ShouldBe("Data tidak ditemukan. Server: Farmer was not found");

    /// <summary>
    /// Every static <see cref="Error"/> field or property declared in Domain and SharedKernel.
    /// </summary>
    private static HashSet<string> ServerErrorCodes()
    {
        Assembly[] assemblies = [typeof(UserErrors).Assembly, typeof(Error).Assembly];

        IEnumerable<Error?> fields = assemblies
            .SelectMany(a => a.GetTypes())
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .Where(f => typeof(Error).IsAssignableFrom(f.FieldType))
            .Select(f => f.GetValue(null) as Error);

        // Factories such as FarmerErrors.NotFound(Guid): the code does not depend on the arguments.
        IEnumerable<Error?> factories = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsAbstract && t.IsSealed && t.Name.EndsWith("Errors", StringComparison.Ordinal))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => typeof(Error).IsAssignableFrom(m.ReturnType) && !m.IsGenericMethod)
            .Select(Invoke);

        HashSet<string> codes = [.. fields.Concat(factories).OfType<Error>().Select(e => e.Code)];
        codes.Add(new ValidationError([]).Code);

        return codes;
    }

    private static Error? Invoke(MethodInfo method)
    {
        object?[] arguments = [.. method.GetParameters().Select(p => SampleValue(p.ParameterType))];

        try
        {
            return method.Invoke(null, arguments) as Error;
        }
        catch (TargetInvocationException)
        {
            return null;
        }
    }

    private static object? SampleValue(Type type)
    {
        if (type == typeof(string))
        {
            return "x";
        }

        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
