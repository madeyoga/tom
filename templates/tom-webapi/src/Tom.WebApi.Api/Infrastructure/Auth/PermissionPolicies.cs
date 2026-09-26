using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Tom.WebApi.Api.Infrastructure.Auth;

public static class PermissionPolicies
{
    public static AuthorizationBuilder AddPermission(
        this AuthorizationBuilder auth,
        string permission,
        params string[] roles)
        => auth.AddPolicy(permission, policy => policy
            .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme)
            .RequireAuthenticatedUser()
            .RequireRole(roles));
}
