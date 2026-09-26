using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace Tom.WebApi.Api.Tests;

[Collection("api")]
public sealed class OpenApiExposureTests(ApiFactory factory)
{
    [Theory]
    [InlineData(null, HttpStatusCode.NotFound)]
    [InlineData("false", HttpStatusCode.NotFound)]
    [InlineData("true", HttpStatusCode.OK)]
    public async Task Production_serves_openapi_only_with_the_flag(string? flag, HttpStatusCode expected)
    {
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting(
                "AuthEndpoints:EmailConfirmation:ConfirmEmailRedirectUri",
                "https://localhost/confirm-email");
            builder.UseSetting(
                "AuthEndpoints:EmailConfirmation:AllowedRedirectOrigins:0",
                "https://localhost");
            if (flag is not null)
            {
                builder.UseSetting("OpenApi:Enabled", flag);
            }
        });
        var client = app.CreateClient();

        Assert.Equal(expected, (await client.GetAsync("/openapi/v1.json")).StatusCode);
        Assert.Equal(expected, (await client.GetAsync("/scalar/v1")).StatusCode);
    }
}
