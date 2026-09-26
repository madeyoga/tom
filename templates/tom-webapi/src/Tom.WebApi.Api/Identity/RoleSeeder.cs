using Tom.WebApi.Api.Identity.Contracts;
using Tom.WebApi.Api.Shared;
using Microsoft.AspNetCore.Identity;

namespace Tom.WebApi.Api.Identity;

internal sealed class RoleSeeder(RoleManager<AppRole> roleManager) : IDataSeeder
{
    public int Order => 0;

    public bool IsCritical => true;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        foreach (var roleName in AppRoles.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var created = await roleManager.CreateAsync(new AppRole(roleName));
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create role '{roleName}': {string.Join(", ", created.Errors.Select(error => error.Description))}");
            }
        }
    }
}
