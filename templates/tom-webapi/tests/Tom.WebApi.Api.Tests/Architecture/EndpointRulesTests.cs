using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Tom.WebApi.Api.Infrastructure.Data;

namespace Tom.WebApi.Api.Tests.Architecture;

[Collection("api")]
public sealed class EndpointRulesTests(ApiFactory factory)
{
    [Fact]
    public async Task Endpoints_declare_name_summary_tag_and_permission()
    {
        var names = new List<string>();
        var provider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var permissions = PermissionConstants();
        foreach (var endpoint in ApiEndpoints())
        {
            var name = endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName;
            Assert.False(string.IsNullOrWhiteSpace(name), endpoint.RoutePattern.RawText);
            names.Add(name);
            Assert.NotNull(endpoint.Metadata.GetMetadata<IEndpointSummaryMetadata>());
            Assert.NotNull(endpoint.Metadata.GetMetadata<ITagsMetadata>());

            var authorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().ToArray();
            var anonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            if (anonymous)
            {
                continue;
            }

            Assert.NotEmpty(authorize);
            Assert.All(authorize, data =>
            {
                Assert.True(string.IsNullOrEmpty(data.Roles), endpoint.RoutePattern.RawText);
                Assert.False(string.IsNullOrWhiteSpace(data.Policy), endpoint.RoutePattern.RawText);
            });
            foreach (var policyName in authorize.Select(data => data.Policy!).Distinct())
            {
                Assert.NotNull(await provider.GetPolicyAsync(policyName));
                Assert.Contains(policyName, permissions);
            }
        }

        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Unsafe_verbs_require_antiforgery()
    {
        foreach (var endpoint in ApiEndpoints())
        {
            var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [];
            if (!methods.Any(method => method is "POST" or "PUT" or "PATCH" or "DELETE"))
            {
                continue;
            }

            Assert.Contains(
                endpoint.Metadata.GetOrderedMetadata<IAntiforgeryMetadata>(),
                metadata => metadata.RequiresValidation);
        }
    }

    [Fact]
    public void Handlers_return_typed_results_from_api_classes()
    {
        foreach (var endpoint in ApiEndpoints())
        {
            var method = Handler(endpoint);
            var returnType = method.ReturnType;
            Assert.False(IsBareResult(returnType), endpoint.RoutePattern.RawText);
            Assert.Null(method.GetCustomAttribute<CompilerGeneratedAttribute>());
            var declaring = method.DeclaringType!;
            Assert.True(declaring.IsAbstract && declaring.IsSealed);
            Assert.EndsWith("Api", declaring.Name);
        }
    }

    [Fact]
    public void Endpoints_never_expose_entities()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var isService = factory.Services.GetRequiredService<IServiceProviderIsService>();
        var entities = db.Model.GetEntityTypes().Select(entity => entity.ClrType).ToHashSet();

        var failures = new List<string>();
        foreach (var endpoint in ApiEndpoints())
        {
            var method = Handler(endpoint);
            var types = method.GetParameters()
                .Where(parameter => !isService.IsService(parameter.ParameterType)
                    && parameter.ParameterType != typeof(CancellationToken))
                .Select(parameter => parameter.ParameterType)
                .Append(method.ReturnType);
            foreach (var hit in types.SelectMany(type => Reachable(type)).Where(entities.Contains).Distinct())
            {
                failures.Add($"{endpoint.RoutePattern.RawText} exposes {hit.Name}");
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void Module_dtos_are_sealed_records_in_the_handler_module()
    {
        var isService = factory.Services.GetRequiredService<IServiceProviderIsService>();
        var failures = new List<string>();
        foreach (var endpoint in ApiEndpoints())
        {
            var method = Handler(endpoint);
            var handlerModule = Arch.ModuleOf(method.DeclaringType!);
            var types = method.GetParameters()
                .Where(parameter => !isService.IsService(parameter.ParameterType)
                    && parameter.ParameterType != typeof(CancellationToken))
                .Select(parameter => parameter.ParameterType)
                .Append(method.ReturnType);
            foreach (var type in types.SelectMany(item => Reachable(item)).Distinct())
            {
                if (Arch.ModuleOf(type) is not { } module)
                {
                    continue;
                }

                if (type.IsInterface || type.IsAbstract)
                {
                    continue;
                }

                var isRecord = type.GetMethod("<Clone>$") is not null;
                if (!type.IsSealed || !isRecord || type.Name.EndsWith("Dto", StringComparison.Ordinal))
                {
                    failures.Add($"{type.FullName} on {endpoint.RoutePattern.RawText}");
                }

                if (module != handlerModule)
                {
                    failures.Add($"{type.FullName} is used from module {handlerModule}");
                }
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void Collection_posts_return_201()
    {
        Type[] created = [typeof(Created<>), typeof(CreatedAtRoute<>), typeof(Created), typeof(CreatedAtRoute)];
        var failures = new List<string>();
        var checkedCount = 0;
        foreach (var endpoint in ApiEndpoints())
        {
            var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [];
            if (!methods.Contains("POST") || endpoint.RoutePattern.Parameters.Count > 0)
            {
                continue;
            }

            checkedCount++;
            var returnType = Handler(endpoint).ReturnType;
            if (returnType.IsGenericType && (returnType.GetGenericTypeDefinition() == typeof(Task<>)
                || returnType.GetGenericTypeDefinition() == typeof(ValueTask<>)))
            {
                returnType = returnType.GetGenericArguments()[0];
            }

            var members = returnType.IsGenericType && typeof(INestedHttpResult).IsAssignableFrom(returnType)
                ? returnType.GetGenericArguments()
                : [returnType];
            if (!members.Any(type => created.Contains(type.IsGenericType ? type.GetGenericTypeDefinition() : type)))
            {
                failures.Add($"POST {endpoint.RoutePattern.RawText} returns {returnType.Name}, expected Created/CreatedAtRoute");
            }
        }

        Assert.True(checkedCount > 0, "No collection POST endpoints found under /api.");
        Assert.Empty(failures);
    }

    private IEnumerable<RouteEndpoint> ApiEndpoints()
        => factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api", StringComparison.Ordinal) == true);

    private static MethodInfo Handler(RouteEndpoint endpoint)
        => endpoint.Metadata.GetMetadata<MethodInfo>()
            ?? throw new InvalidOperationException($"No handler for {endpoint.RoutePattern.RawText}.");

    private static bool IsBareResult(Type type)
    {
        if (type == typeof(IResult) || type == typeof(Task<IResult>) || type == typeof(ValueTask<IResult>))
        {
            return true;
        }

        return false;
    }

    private static HashSet<string> PermissionConstants()
        => Arch.Types
            .Where(type => type.Name.EndsWith("Permissions", StringComparison.Ordinal))
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<Type> Reachable(Type type, HashSet<Type>? seen = null)
    {
        seen ??= [];
        if (!seen.Add(type) || type.IsPrimitive || type == typeof(string)
            || (type.Namespace?.StartsWith("System", StringComparison.Ordinal) == true && !type.IsGenericType))
        {
            yield break;
        }

        yield return type;
        IEnumerable<Type> next = type.IsArray
            ? [type.GetElementType()!]
            : type.GetGenericArguments().Concat(
                type.Namespace?.StartsWith(Arch.Root, StringComparison.Ordinal) == true
                    ? type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.PropertyType)
                    : []);
        foreach (var child in next)
        {
            foreach (var reachable in Reachable(child, seen))
            {
                yield return reachable;
            }
        }
    }
}
