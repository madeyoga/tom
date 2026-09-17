using Tom.WebApi.Identity;
using Microsoft.AspNetCore.Identity;

namespace Tom.WebApi.Data;

public static class AppDbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IHostEnvironment environment)
    {
        var roleManager = services.GetRequiredService<RoleManager<AppRole>>();
        foreach (var roleName in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var created = await roleManager.CreateAsync(new AppRole(roleName));
                if (!created.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Failed to create role '{roleName}': {string.Join(", ", created.Errors.Select(e => e.Description))}");
                }
            }
        }

        if (environment.IsDevelopment())
        {
            await SeedDevelopmentAdminAsync(services);
            return;
        }

        await TryBootstrapProductionAdminAsync(services);
    }

    private static async Task SeedDevelopmentAdminAsync(IServiceProvider services)
    {
        var config = services.GetRequiredService<IConfiguration>();
        var password = config["Seed:Password"];
        if (string.IsNullOrWhiteSpace(password))
        {
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AppDbSeeder));
            logger.LogWarning(
                "Seed:Password is not set. Skipping Development Admin seed. Copy .env.example to .env and set Seed__Password.");
            return;
        }

        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        await EnsureUserAsync(
            userManager,
            config["Seed:AdminEmail"] ?? "admin@localhost",
            password,
            AppRoles.Admin);
    }

    private static async Task TryBootstrapProductionAdminAsync(IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AppDbSeeder));
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var admins = await userManager.GetUsersInRoleAsync(AppRoles.Admin);
        if (admins.Count > 0)
        {
            return;
        }

        var config = services.GetRequiredService<IConfiguration>();
        var email = config["Seed:BootstrapAdminEmail"];
        var password = config["Seed:BootstrapAdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "No Admin user exists. Set Seed__BootstrapAdminEmail and Seed__BootstrapAdminPassword, then restart the API to create the first Admin. POST /identity/register does not assign roles.");
            return;
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
            };
            var created = await userManager.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to bootstrap Admin '{email}': {string.Join(", ", created.Errors.Select(e => e.Description))}");
            }
        }
        else if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            var updated = await userManager.UpdateAsync(user);
            if (!updated.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to confirm bootstrap Admin '{email}': {string.Join(", ", updated.Errors.Select(e => e.Description))}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, AppRoles.Admin))
        {
            var added = await userManager.AddToRoleAsync(user, AppRoles.Admin);
            if (!added.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to assign Admin to '{email}': {string.Join(", ", added.Errors.Select(e => e.Description))}");
            }
        }

        logger.LogInformation(
            "Bootstrapped the first Admin ({Email}). Change this password after first login.",
            email);
    }

    private static async Task EnsureUserAsync(
        UserManager<AppUser> userManager,
        string email,
        string password,
        string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
            };
            var created = await userManager.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to seed user '{email}': {string.Join(", ", created.Errors.Select(e => e.Description))}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            var added = await userManager.AddToRoleAsync(user, role);
            if (!added.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to assign role '{role}' to '{email}': {string.Join(", ", added.Errors.Select(e => e.Description))}");
            }
        }
    }
}
