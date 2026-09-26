using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Tom.WebApi.Api.Infrastructure.Data;
using Tom.WebApi.Api.Shared;

namespace Tom.WebApi.Api.Tests.Architecture;

[Collection("api")]
public sealed class HostRulesTests(ApiFactory factory)
{
    [Fact]
    public void Service_graph_is_valid()
    {
        using var scope = factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    [Fact]
    public void No_transient_disposable_from_the_api_assembly()
    {
        List<ServiceDescriptor> captured = [];
        using var app = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => captured = services.ToList()));
        _ = app.Services;
        var bad = captured
            .Where(descriptor => descriptor.Lifetime == ServiceLifetime.Transient)
            .Select(descriptor => descriptor.ImplementationType)
            .Where(type => type is not null && type.Assembly == Arch.Api)
            .Where(type => typeof(IDisposable).IsAssignableFrom(type) || typeof(IAsyncDisposable).IsAssignableFrom(type))
            .Select(type => type!.FullName);
        Assert.Empty(bad);
    }

    [Fact]
    public void Every_seeder_is_registered_scoped_with_a_distinct_order()
    {
        using var scope = factory.Services.CreateScope();
        var seeders = scope.ServiceProvider.GetServices<IDataSeeder>().ToArray();
        var implementations = Arch.Types
            .Where(type => typeof(IDataSeeder).IsAssignableFrom(type) && type.IsClass && !type.IsAbstract)
            .ToArray();
        Assert.Equal(implementations.Length, seeders.Length);
        Assert.Equal(seeders.Length, seeders.Select(seeder => seeder.Order).Distinct().Count());
        foreach (var implementation in implementations)
        {
            Assert.Contains(seeders, seeder => seeder.GetType() == implementation);
        }
    }

    [Fact]
    public void Navigations_stay_inside_a_module()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var failures = new List<string>();
        foreach (var entity in db.Model.GetEntityTypes())
        {
            foreach (var navigation in entity.GetNavigations().Cast<INavigationBase>().Concat(entity.GetSkipNavigations()))
            {
                var left = ModuleOfEntity(entity.ClrType);
                var right = ModuleOfEntity(navigation.TargetEntityType.ClrType);
                if (left != right)
                {
                    failures.Add($"{entity.ClrType.Name}.{navigation.Name} -> {navigation.TargetEntityType.ClrType.Name}");
                }
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void Each_module_entity_has_one_configuration_and_guid_keys_are_app_generated()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var entity in db.Model.GetEntityTypes())
        {
            if (Arch.ModuleOf(entity.ClrType) is not { } module)
            {
                continue;
            }

            var configs = Arch.Types.Where(type => type.GetInterfaces().Any(iface =>
                iface.IsGenericType
                && iface.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)
                && iface.GenericTypeArguments[0] == entity.ClrType)).ToArray();
            Assert.Single(configs);
            Assert.Equal(module, Arch.ModuleOf(configs[0]));

            var key = entity.FindPrimaryKey();
            if (key is { Properties.Count: 1 } && key.Properties[0].ClrType == typeof(Guid))
            {
                Assert.Equal(ValueGenerated.Never, key.Properties[0].ValueGenerated);
            }
        }
    }

    [Fact]
    public async Task Anonymous_gets_401_and_a_user_without_permission_gets_403()
    {
        var client = factory.AnonymousClient();
        var checkedGet = 0;
        foreach (var endpoint in ApiEndpoints())
        {
            var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [];
            if (!methods.Contains("GET"))
            {
                continue;
            }

            var authorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
            if (!authorize.Any(data => !string.IsNullOrWhiteSpace(data.Policy)))
            {
                continue;
            }

            checkedGet++;
            var path = FillRoute(endpoint);
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        Assert.True(checkedGet > 0);
        var forbidden = await (await factory.UnprivilegedClientAsync()).GetAsync("/api/notes");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Missing_api_route_is_a_problem()
    {
        var response = await factory.AnonymousClient().GetAsync("/api/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private IEnumerable<RouteEndpoint> ApiEndpoints()
        => factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api", StringComparison.Ordinal) == true);

    private static string FillRoute(RouteEndpoint endpoint)
    {
        var path = endpoint.RoutePattern.RawText ?? "/";
        foreach (var parameter in endpoint.RoutePattern.Parameters)
        {
            var match = Regex.Match(path, $"\\{{{Regex.Escape(parameter.Name)}(?::[^}}]+)?\\}}");
            var replacement = parameter.ParameterPolicies.Any(policy =>
                policy.Content?.Contains("guid", StringComparison.OrdinalIgnoreCase) == true)
                ? Guid.CreateVersion7().ToString()
                : "1";
            path = match.Success
                ? path.Replace(match.Value, replacement, StringComparison.Ordinal)
                : path;
        }

        return path;
    }

    private static string ModuleOfEntity(Type type)
    {
        if (Arch.ModuleOf(type) is { } module)
        {
            return module;
        }

        if (type.Namespace?.StartsWith("Microsoft.AspNetCore.Identity", StringComparison.Ordinal) == true)
        {
            return "Identity";
        }

        return type.FullName ?? type.Name;
    }
}
