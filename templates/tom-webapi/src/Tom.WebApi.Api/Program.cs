using AuthEndpoints;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Tom.WebApi.Api.Data;
using Tom.WebApi.Api.Identity;
using Tom.WebApi.Api.Infrastructure;
using Tom.WebApi.Api.Notes;

if (string.Equals(
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
        "Development",
        StringComparison.OrdinalIgnoreCase)
    && File.Exists(".env"))
{
    DotNetEnv.Env.Load();
}

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseDefaultServiceProvider(o =>
{
    o.ValidateScopes = true;
    o.ValidateOnBuild = true;
});

builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddNotesModule(builder.Configuration);

// Export stays off unless OTEL_EXPORTER_OTLP_ENDPOINT is set, so CI and production are unchanged.
ConfigureOpenTelemetry(builder);

var app = builder.Build();

if (app.Environment.IsProduction())
{
    app.UseForwardedHeaders();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseCors("Frontend");
}

app.UseAuthEndpoints();

// DOC-02: Development, or OpenApi:Enabled=true on a test/staging server. Never on real production.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("OpenApi:Enabled"))
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.Title = "Tom.WebApi");
}

app.MapAuthEndpoints<AppUser>().WithTags("Authentication & Authorization");

var api = app.MapGroup("/api");
api.MapIdentityModule();
api.MapNotesModule();

app.MapHealthChecks("/health");
app.Run();

static void ConfigureOpenTelemetry(WebApplicationBuilder builder)
{
    var endpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
    if (string.IsNullOrWhiteSpace(endpoint))
    {
        return;
    }

    var serviceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME");
    if (string.IsNullOrWhiteSpace(serviceName))
    {
        serviceName = "Tom.WebApi";
    }

    // db.statement is the SQL text. Do not record parameter values.
    Environment.SetEnvironmentVariable("OTEL_DOTNET_EXPERIMENTAL_EFCORE_ENABLE_TRACE_DB_QUERY_PARAMETERS", "false");

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(serviceName))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddOtlpExporter());
}

public partial class Program;
