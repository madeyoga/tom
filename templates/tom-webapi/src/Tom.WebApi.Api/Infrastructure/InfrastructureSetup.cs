using System.Text.Json.Serialization;
using AuthEndpoints;
using AuthEndpoints.Passkey;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Tom.WebApi.Api.Data;
using Tom.WebApi.Api.Shared;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
            options.AddSchemaTransformer((schema, context, _) =>
            {
                if (context.JsonTypeInfo.Type != typeof(NoteId))
                {
                    return Task.CompletedTask;
                }

                // The JSON converter writes a Guid string. Describe that, not the record shape.
                schema.Type = JsonSchemaType.String;
                schema.Format = "uuid";
                schema.Properties?.Clear();
                schema.Required?.Clear();
                return Task.CompletedTask;
            });
        });

        services.AddDbContext<AppDbContext>((sp, options) => options.UseNpgsql(
            sp.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured."),
            npgsql => npgsql.SetPostgresVersion(17, 0)));

        var identity = services.AddPasswordAuth<AppUser, AppDbContext>(o =>
        {
            o.Session = AuthEndpointsSession.Cookie;
            o.Path = "/auth";
            o.RequireConfirmedAccount = config.GetValue("AuthEndpoints:RequireConfirmedAccount", true);
            if (config.GetValue("AuthEndpoints:Passkeys:Enabled", true))
            {
                var serverDomain = config["AuthEndpoints:Passkeys:ServerDomain"];
                o.Passkeys.ServerDomain = string.IsNullOrWhiteSpace(serverDomain) ? "localhost" : serverDomain;
            }

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
        });

        // AddPasswordAuth has no role overload. It registers a user store that does not
        // implement IUserRoleStore, and AddEntityFrameworkStores uses TryAdd, so that
        // store and the role-less claims factory have to be removed before roles are stored.
        services.RemoveAll<IUserClaimsPrincipalFactory<AppUser>>();
        services.RemoveAll<IUserStore<AppUser>>();
        identity.AddRoles<AppRole>().AddEntityFrameworkStores<AppDbContext>();

        services.Configure<AuthEndpointsReAuthOptions>(options =>
        {
            var reauthLifetime = config["AuthEndpoints:ReAuth:Lifetime"];
            if (TimeSpan.TryParse(reauthLifetime, out var lifetime))
            {
                options.Lifetime = lifetime;
            }
        });
        services.AddPasskeyUserIdFactory(() => Guid.CreateVersion7().ToString());

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
