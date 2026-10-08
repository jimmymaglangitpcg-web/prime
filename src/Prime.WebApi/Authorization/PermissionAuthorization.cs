using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Prime.Application.Common.Security;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Offices;
using Prime.Application.Features.Security;
using Prime.WebApi.Contracts;

namespace Prime.WebApi.Authorization;

/// <summary>The permission an API action requires (docs/analysis/workflow-security.md §4.1).</summary>
public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

/// <summary>
/// Declares the permission an action needs. Every API action carries one, or <see cref="SignedInOnlyAttribute"/>; an
/// action with neither is refused (<see cref="PermissionDeclarationConvention"/>). It runs before the services' own checks
/// (maker-checker, approval chains, jurisdiction), never instead of them.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute(string permission) : AuthorizeAttribute, IAuthorizationRequirementData
{
    public string Permission { get; } = permission;

    public IEnumerable<IAuthorizationRequirement> GetRequirements() => [new PermissionRequirement(Permission)];
}

/// <summary>An action any signed-in user may call, assigned or not (e.g. who am I).</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SignedInOnlyAttribute : Attribute;

/// <summary>
/// Grants a <see cref="PermissionRequirement"/> when the user's roles hold the permission and, for the roles that need
/// one, the sign-in had a second factor (workflow-security.md §4.2, Q7). Signed-in-only actions need neither, so a user
/// can still learn who they are and enrol a factor.
/// </summary>
public sealed class PermissionAuthorizationHandler(IPermissionService permissions, IOfficeContext offices, ICurrentUserService currentUser,
    SecuritySettings security) : AuthorizationHandler<PermissionRequirement>
{
    public const string MfaRequired = "MFA_REQUIRED";

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true || requirement.Permission == Permissions.Undeclared
            || !await permissions.HasAsync(requirement.Permission))
        {
            return;
        }
        var scope = await offices.GetAsync();
        if (!security.MfaSatisfied(scope.Roles, currentUser.AssuranceLevel))
        {
            context.Fail(new AuthorizationFailureReason(this, MfaRequired));
            return;
        }
        context.Succeed(requirement);
    }
}

/// <summary>
/// Refuses, at start-up, the actions that declare no permission (deny by default): it adds the never-granted
/// <see cref="Permissions.Undeclared"/> requirement to them. A test also lists every action and fails on any such one.
/// </summary>
public sealed class PermissionDeclarationConvention : IActionModelConvention
{
    public void Apply(ActionModel action)
    {
        var declared = action.Attributes.OfType<RequirePermissionAttribute>().Any()
            || action.Attributes.OfType<SignedInOnlyAttribute>().Any()
            || action.Controller.Attributes.OfType<RequirePermissionAttribute>().Any();
        if (declared)
        {
            return;
        }
        foreach (var selector in action.Selectors)
        {
            selector.EndpointMetadata.Add(new RequirePermissionAttribute(Permissions.Undeclared));
        }
    }
}

/// <summary>A refused permission answers 403 in the CLAUDE.md §63 shape, naming what was missing.</summary>
public sealed class PermissionResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler fallback = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden && context.User.Identity?.IsAuthenticated == true
            && authorizeResult.AuthorizationFailure?.FailureReasons.Any(r => r.Message == PermissionAuthorizationHandler.MfaRequired) == true)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ApiError(PermissionAuthorizationHandler.MfaRequired,
                "Your role needs sign-in with a second factor (an authenticator app). Sign in again and complete it.", null,
                Activity.Current?.Id ?? context.TraceIdentifier));
            return;
        }
        if (authorizeResult.Forbidden && context.User.Identity?.IsAuthenticated == true)
        {
            var missing = authorizeResult.AuthorizationFailure?.FailedRequirements.OfType<PermissionRequirement>().Select(r => r.Permission).ToList() ?? [];
            var undeclared = missing.Contains(Permissions.Undeclared);
            var error = new ApiError(
                undeclared ? "PERMISSION_NOT_DECLARED" : "PERMISSION_DENIED",
                undeclared
                    ? "This action declares no permission and is refused."
                    : $"You do not have the permission this action needs: {string.Join(", ", missing)}.",
                null,
                Activity.Current?.Id ?? context.TraceIdentifier);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(error);
            return;
        }
        await fallback.HandleAsync(next, context, policy, authorizeResult);
    }
}
