using AuthEndpoints;
using Scalar.AspNetCore;
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

public partial class Program;
