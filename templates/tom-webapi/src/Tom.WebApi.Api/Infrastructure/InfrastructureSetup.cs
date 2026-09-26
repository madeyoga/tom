using System.Text.Json.Serialization;
using AuthEndpoints;
using AuthEndpoints.Passkey;
using Microsoft.AspNetCore.Antiforgery;
using Tom.WebApi.Api.Identity;
using Tom.WebApi.Api.Infrastructure.Data;
using Tom.WebApi.Api.Infrastructure.Seeding;
using Tom.WebApi.Api.Shared;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

namespace Tom.WebApi.Api.Infrastructure;

public static class InfrastructureSetup
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration config,
        IHostEnvironment env)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddScoped<CurrentUser>();
        services.AddHealthChecks();
        services.AddProblemDetails();
        services.AddValidation();
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Tom.WebApi",
                    Version = "v1",
                };
                return Task.CompletedTask;
            });
        });

        services.AddDbContext<AppDbContext>((sp, options) => options.UseNpgsql(
            sp.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured."),
            npgsql => npgsql.SetPostgresVersion(17, 0)));

        services.AddAuthEndpoints<AppUser, AppRole, AppDbContext>(o =>
        {
            o.Passkeys.Enabled = config.GetValue("AuthEndpoints:Passkeys:Enabled", true);
            o.Passkeys.ServerDomain = config["AuthEndpoints:Passkeys:ServerDomain"] ?? "localhost";
            o.RequireConfirmedAccount = config.GetValue("AuthEndpoints:RequireConfirmedAccount", true);
            o.Jwt.Enabled = config.GetValue("AuthEndpoints:Jwt:Enabled", false);
            o.EmailConfirmation.ConfirmEmailRedirectUri =
                config["AuthEndpoints:EmailConfirmation:ConfirmEmailRedirectUri"]
                ?? "http://localhost:3000/confirm-email";

            o.EmailConfirmation.AllowedRedirectOrigins.Clear();
            var allowedOrigins = config
                .GetSection("AuthEndpoints:EmailConfirmation:AllowedRedirectOrigins")
                .Get<string[]>();
            if (allowedOrigins is { Length: > 0 })
            {
                foreach (var origin in allowedOrigins)
                {
                    o.EmailConfirmation.AllowedRedirectOrigins.Add(origin);
                }
            }
            else
            {
                o.EmailConfirmation.AllowedRedirectOrigins.Add("http://localhost:3000");
            }

            var reauthLifetime = config["AuthEndpoints:ReAuth:Lifetime"];
            if (TimeSpan.TryParse(reauthLifetime, out var lifetime))
            {
                o.ReAuth.Lifetime = lifetime;
            }
        });
        services.AddPasskeyUserIdFactory(() => Guid.CreateVersion7().ToString());

        services.AddTransient<IEmailSender<AppUser>, FileEmailSender>();
        services.Configure<AntiforgeryOptions>(options =>
        {
            options.HeaderName = "RequestVerificationToken";
        });

        var keysPath = config["DataProtection:KeysPath"];
        if (string.IsNullOrWhiteSpace(keysPath))
        {
            keysPath = Path.Combine(env.ContentRootPath, "keys");
        }

        Directory.CreateDirectory(keysPath);
        services.AddDataProtection()
            .SetApplicationName("Tom.WebApi")
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

        services.ConfigureApplicationCookie(options =>
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

        if (env.IsProduction())
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });
        }

        if (env.IsDevelopment())
        {
            var frontendOrigin = config["Frontend:Origin"] ?? "http://localhost:3000";
            services.AddCors(options =>
            {
                options.AddPolicy("Frontend", policy =>
                {
                    policy
                        .WithOrigins(frontendOrigin)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });
        }

        services.AddHostedService<DatabaseInitializer>();
        return services;
    }
}
