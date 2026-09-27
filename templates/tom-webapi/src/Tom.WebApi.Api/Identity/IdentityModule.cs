using Microsoft.AspNetCore.Identity;
using Tom.WebApi.Api.Data;
using Tom.WebApi.Api.Infrastructure;
using Tom.WebApi.Api.Shared;

namespace Tom.WebApi.Api.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        _ = configuration;
        services.AddScoped<IDataSeeder, RoleSeeder>();
        services.AddScoped<IDataSeeder, AdminUserSeeder>();
        services.AddTransient<IEmailSender<AppUser>, FileEmailSender>();
        services.AddAuthorizationBuilder()
            .AddPermission(IdentityPermissions.Access, AppRoles.Admin);
        return services;
    }

    public static IEndpointRouteBuilder MapIdentityModule(this IEndpointRouteBuilder endpoints) => endpoints;
}
