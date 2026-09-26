using Tom.WebApi.Api.Infrastructure.Data;
using Tom.WebApi.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace Tom.WebApi.Api.Infrastructure.Seeding;

internal sealed class DatabaseInitializer(
    IServiceScopeFactory scopes,
    IHostEnvironment environment,
    IConfiguration configuration,
    ILogger<DatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (environment.IsDevelopment() || configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
            await db.Database.MigrateAsync(cancellationToken);
        }

        var seeders = scope.ServiceProvider.GetServices<IDataSeeder>().OrderBy(seeder => seeder.Order);
        foreach (var seeder in seeders)
        {
            try
            {
                await seeder.SeedAsync(cancellationToken);
            }
            catch (Exception ex) when (!seeder.IsCritical)
            {
                logger.LogError(ex, "Seeder {Seeder} failed.", seeder.GetType().Name);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
