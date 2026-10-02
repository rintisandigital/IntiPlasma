using System.Reflection;
using Application.Abstractions.Messaging;
using Domain.Users;
using Infrastructure.Database;
using Web.Api;

namespace ArchitectureTests;

public abstract class BaseTest
{
    protected static readonly Assembly DomainAssembly = typeof(User).Assembly;
    protected static readonly Assembly ApplicationAssembly = typeof(ICommand).Assembly;
    protected static readonly Assembly InfrastructureAssembly = typeof(ApplicationDbContext).Assembly;
    protected static readonly Assembly PresentationAssembly = typeof(Web.Api.Program).Assembly;
    protected static readonly Assembly WebAppAssembly = typeof(Web.App.Program).Assembly;
}
