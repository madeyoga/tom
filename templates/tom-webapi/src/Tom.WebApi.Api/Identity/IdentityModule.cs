using Tom.WebApi.Api.Identity.Contracts;
using Tom.WebApi.Api.Infrastructure.Auth;
using Tom.WebApi.Api.Shared;

namespace Tom.WebApi.Api.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        _ = configuration;
        services.AddScoped<IDataSeeder, RoleSeeder>();
        services.AddScoped<IDataSeeder, AdminUserSeeder>();
        services.AddAuthorizationBuilder()
            .AddPermission(IdentityPermissions.Access, AppRoles.Admin);
        return services;
    }

    public static IEndpointRouteBuilder MapIdentityModule(this IEndpointRouteBuilder endpoints) => endpoints;
}
