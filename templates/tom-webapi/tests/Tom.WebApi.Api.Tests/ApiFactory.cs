using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Tom.WebApi.Api.Identity;
using Testcontainers.PostgreSql;

namespace Tom.WebApi.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@example.test";
    public const string AdminPassword = "Test!Passw0rd";
    public const string UnprivilegedEmail = "user@example.test";
    public const string UnprivilegedPassword = "Test!Passw0rd";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly SemaphoreSlim _signInGate = new(1, 1);
    private HttpClient? _admin;
    private HttpClient? _unprivileged;

    public async Task InitializeAsync() => await _db.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        _admin?.Dispose();
        _unprivileged?.Dispose();
        _signInGate.Dispose();
        await DisposeAsync();
        await _db.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _db.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Seed:AdminEmail", AdminEmail);
        builder.UseSetting("Seed:Password", AdminPassword);
        builder.UseSetting(
            "DataProtection:KeysPath",
            Path.Combine(Path.GetTempPath(), "tom-webapi-tests", Guid.CreateVersion7().ToString("N"), "keys"));
    }

    public async Task<HttpClient> AdminClientAsync()
    {
        if (_admin is not null)
        {
            return _admin;
        }

        await _signInGate.WaitAsync();
        try
        {
            _admin ??= await SignInAsync(this, AdminEmail, AdminPassword);
            return _admin;
        }
        finally
        {
            _signInGate.Release();
        }
    }

    public async Task<HttpClient> UnprivilegedClientAsync()
    {
        if (_unprivileged is not null)
        {
            return _unprivileged;
        }

        await _signInGate.WaitAsync();
        try
        {
            if (_unprivileged is null)
            {
                await EnsureUserWithoutRolesAsync(UnprivilegedEmail, UnprivilegedPassword);
                _unprivileged = await SignInAsync(this, UnprivilegedEmail, UnprivilegedPassword);
            }

            return _unprivileged;
        }
        finally
        {
            _signInGate.Release();
        }
    }

    public static async Task<HttpClient> SignInAsync(WebApplicationFactory<Program> app, string email, string password)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false,
        });
        (await client.PostAsJsonAsync("/identity/login", new { email, password })).EnsureSuccessStatusCode();
        var csrf = await client.GetFromJsonAsync<CsrfTokenResponse>("/identity/csrfToken");
        client.DefaultRequestHeaders.Add("RequestVerificationToken", csrf!.CsrfToken);
        return client;
    }

    public HttpClient AnonymousClient()
        => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task EnsureUserWithoutRolesAsync(string email, string password)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
        {
            return;
        }

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(", ", created.Errors.Select(error => error.Description)));
        }
    }

    private sealed record CsrfTokenResponse(string CsrfToken);
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;
