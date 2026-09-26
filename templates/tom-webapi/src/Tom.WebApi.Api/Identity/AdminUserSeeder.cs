using Tom.WebApi.Api.Identity.Contracts;
using Tom.WebApi.Api.Shared;
using Microsoft.AspNetCore.Identity;

namespace Tom.WebApi.Api.Identity;

internal sealed class AdminUserSeeder(
    UserManager<AppUser> userManager,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<AdminUserSeeder> logger) : IDataSeeder
{
    public int Order => 10;

    public bool IsCritical => true;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var password = configuration["Seed:Password"];
        if (!string.IsNullOrWhiteSpace(password))
        {
            await EnsureUserAsync(
                configuration["Seed:AdminEmail"] ?? "admin@localhost",
                password,
                cancellationToken);
            return;
        }

        if (environment.IsDevelopment())
        {
            logger.LogWarning(
                "Seed:Password is not set. Skipping Development Admin seed. Copy .env.example to .env and set Seed__Password.");
            return;
        }

        await TryBootstrapProductionAdminAsync(cancellationToken);
    }

    private async Task TryBootstrapProductionAdminAsync(CancellationToken cancellationToken)
    {
        var admins = await userManager.GetUsersInRoleAsync(AppRoles.Admin);
        cancellationToken.ThrowIfCancellationRequested();
        if (admins.Count > 0)
        {
            return;
        }

        var email = configuration["Seed:BootstrapAdminEmail"];
        var password = configuration["Seed:BootstrapAdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "No Admin user exists. Set Seed__BootstrapAdminEmail and Seed__BootstrapAdminPassword, then restart the API to create the first Admin. POST /identity/register does not assign roles.");
            return;
        }

        var user = await userManager.FindByEmailAsync(email);
        cancellationToken.ThrowIfCancellationRequested();
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
                    $"Failed to bootstrap Admin '{email}': {string.Join(", ", created.Errors.Select(error => error.Description))}");
            }
        }
        else if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            var updated = await userManager.UpdateAsync(user);
            if (!updated.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to confirm bootstrap Admin '{email}': {string.Join(", ", updated.Errors.Select(error => error.Description))}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, AppRoles.Admin))
        {
            var added = await userManager.AddToRoleAsync(user, AppRoles.Admin);
            if (!added.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to assign Admin to '{email}': {string.Join(", ", added.Errors.Select(error => error.Description))}");
            }
        }

        logger.LogInformation(
            "Bootstrapped the first Admin ({Email}). Change this password after first login.",
            email);
    }

    private async Task EnsureUserAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        cancellationToken.ThrowIfCancellationRequested();
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
                    $"Failed to seed user '{email}': {string.Join(", ", created.Errors.Select(error => error.Description))}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, AppRoles.Admin))
        {
            var added = await userManager.AddToRoleAsync(user, AppRoles.Admin);
            if (!added.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to assign role '{AppRoles.Admin}' to '{email}': {string.Join(", ", added.Errors.Select(error => error.Description))}");
            }
        }
    }
}
