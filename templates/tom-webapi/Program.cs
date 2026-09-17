using System.Text.Json.Serialization;
using AuthEndpoints;
using Tom.WebApi.Data;
using Tom.WebApi.Identity;
using Tom.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

if (string.Equals(
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
        "Development",
        StringComparison.OrdinalIgnoreCase)
    && File.Exists(".env"))
{
    DotNetEnv.Env.Load();
}

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsProduction())
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "Tom.WebApi",
            Version = "v1",
        };
        return Task.CompletedTask;
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddAuthEndpoints<AppUser, AppRole, AppDbContext>(o =>
{
    o.Passkeys.Enabled = builder.Configuration.GetValue("AuthEndpoints:Passkeys:Enabled", false);
    o.Passkeys.ServerDomain = builder.Configuration["AuthEndpoints:Passkeys:ServerDomain"] ?? "localhost";
    o.RequireConfirmedAccount = builder.Configuration.GetValue("AuthEndpoints:RequireConfirmedAccount", true);
    o.Jwt.Enabled = builder.Configuration.GetValue("AuthEndpoints:Jwt:Enabled", false);
});

builder.Services.AddTransient<IEmailSender<AppUser>, FileEmailSender>();

var dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "keys");
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services.AddDataProtection()
    .SetApplicationName("Tom.WebApi")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

var authorization = builder.Services.AddAuthorizationBuilder();
AddRolePolicy(authorization, Permissions.Admin.Access, [AppRoles.Admin]);

var app = builder.Build();

if (app.Environment.IsProduction())
{
    app.UseForwardedHeaders();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthEndpoints();

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "Tom.WebApi";
});

app.MapAuthEndpoints<AppUser>().WithTags("Authentication & Authorization");

var api = app.MapGroup("/api");

app.MapHealthChecks("/health");

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (app.Environment.IsDevelopment())
    {
        await db.Database.MigrateAsync();
    }

    await AppDbSeeder.SeedAsync(scope.ServiceProvider, app.Environment);
}

app.Run();

static void AddRolePolicy(AuthorizationBuilder authorization, string name, string[] roles)
{
    authorization.AddPolicy(name, policy =>
    {
        policy.AddAuthenticationSchemes(IdentityConstants.ApplicationScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireRole(roles);
    });
}
