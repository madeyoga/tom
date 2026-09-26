using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;

namespace Tom.WebApi.Api.Infrastructure.Data;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        if (!File.Exists(Path.Combine(basePath, "appsettings.json")))
        {
            basePath = AppContext.BaseDirectory;
        }

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.SetPostgresVersion(17, 0))
            .UseApplicationServiceProvider(new IdentitySchemaProvider())
            .Options;

        return new AppDbContext(options);
    }

    /// <summary>
    /// AuthEndpoints sets <see cref="StoreOptions.SchemaVersion"/> to 3 at runtime so the model includes
    /// passkeys. Design-time has no host, so this provider supplies the same options. A hand-rolled
    /// <see cref="IServiceProvider"/> avoids <c>BuildServiceProvider</c> (DI-07).
    /// </summary>
    private sealed class IdentitySchemaProvider : IServiceProvider
    {
        private readonly IOptions<IdentityOptions> _identity = Options.Create(new IdentityOptions
        {
            Stores =
            {
                SchemaVersion = IdentitySchemaVersions.Version3,
            },
        });

        public object? GetService(Type serviceType) =>
            serviceType == typeof(IOptions<IdentityOptions>) ? _identity : null;
    }
}
