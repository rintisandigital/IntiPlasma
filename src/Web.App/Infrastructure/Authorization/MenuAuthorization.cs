using Application.Abstractions.Authorization;
using Domain.Access;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Web.App.Infrastructure.Auth;

namespace Web.App.Infrastructure.Authorization;

internal sealed class MenuRequirement(string code, MenuRights right) : IAuthorizationRequirement
{
    public string Code { get; } = code;

    public MenuRights Right { get; } = right;
}

/// <summary>
/// Builds the "menu:{code}:{right}" policies on demand; other policy names go to the default provider.
/// </summary>
internal sealed class MenuPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(MenuAccessAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            return await base.GetPolicyAsync(policyName);
        }

        string body = policyName[MenuAccessAttribute.PolicyPrefix.Length..];
        int separator = body.LastIndexOf(':');

        if (separator <= 0 || !Enum.TryParse(body[(separator + 1)..], out MenuRights right))
        {
            return null;
        }

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new MenuRequirement(body[..separator], right))
            .Build();
    }
}

internal sealed class MenuAuthorizationHandler(IMenuRights menuRights) : AuthorizationHandler<MenuRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, MenuRequirement requirement)
    {
        if (await menuRights.CanAsync(requirement.Code, requirement.Right))
        {
            context.Succeed(requirement);
        }
    }
}

/// <summary>
/// Menu rights of the current user, for policies, tag helpers and views (loaded once per request).
/// </summary>
public interface IMenuRights
{
    Task<bool> CanAsync(string menuCode, MenuRights right = MenuRights.View);

    Task<MenuAccess> GetAsync();
}

internal sealed class MenuRightsService(IHttpContextAccessor httpContextAccessor, IMenuAccessProvider provider) : IMenuRights
{
    private MenuAccess? _access;

    public async Task<bool> CanAsync(string menuCode, MenuRights right = MenuRights.View) =>
        (await GetAsync()).Has(menuCode, right);

    public async Task<MenuAccess> GetAsync()
    {
        if (_access is not null)
        {
            return _access;
        }

        HttpContext? httpContext = httpContextAccessor.HttpContext;
        Guid? userId = httpContext?.User.GetUserId();

        _access = userId is null
            ? MenuAccess.None
            : await provider.GetForUserAsync(userId.Value, httpContext!.RequestAborted);

        return _access;
    }
}
