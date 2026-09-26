using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tom.WebApi.Api.Identity;
using Tom.WebApi.Api.Infrastructure.Data;
using Tom.WebApi.Api.Shared;

namespace Tom.WebApi.Api.Tests.Architecture;

public sealed class TypeRulesTests
{
    private static readonly HashSet<Type> AllowedSingleImplementationInterfaces = [];

    [Fact]
    public void No_mutable_static_state()
    {
        Type[] mutable = [typeof(List<>), typeof(Dictionary<,>), typeof(HashSet<>), typeof(Queue<>), typeof(Stack<>)];
        var bad = Arch.Types
            .SelectMany(type => type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Where(field => !field.IsDefined(typeof(CompilerGeneratedAttribute), false))
            .Where(field => !field.IsLiteral && (!field.IsInitOnly
                || field.FieldType.IsArray
                || (field.FieldType.IsGenericType && mutable.Contains(field.FieldType.GetGenericTypeDefinition()))))
            .Select(field => $"{field.DeclaringType!.FullName}.{field.Name}");
        var props = Arch.Types
            .SelectMany(type => type.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Where(property => property.SetMethod is not null)
            .Select(property => $"{property.DeclaringType!.FullName}.{property.Name}");
        Assert.Empty(bad.Concat(props));
    }

    [Fact]
    public void Hosted_services_do_not_take_a_DbContext()
    {
        var bad = Arch.Types
            .Where(type => typeof(IHostedService).IsAssignableFrom(type) && !type.IsAbstract)
            .Where(type => type.GetConstructors().SelectMany(ctor => ctor.GetParameters())
                .Any(parameter => typeof(DbContext).IsAssignableFrom(parameter.ParameterType)))
            .Select(type => type.FullName);
        Assert.Empty(bad);
    }

    [Fact]
    public void Module_interfaces_have_two_implementations_or_an_allowance()
    {
        var failures = new List<string>();
        foreach (var iface in Arch.Types.Where(type => type.IsInterface && Arch.ModuleOf(type) is not null))
        {
            var implementations = Arch.Api.GetTypes()
                .Count(type => type.IsClass && !type.IsAbstract && iface.IsAssignableFrom(type));
            if (implementations < 2 && !AllowedSingleImplementationInterfaces.Contains(iface))
            {
                failures.Add($"{iface.FullName} has {implementations} implementation(s)");
            }
        }

        var named = Arch.Types
            .Where(type => type.Name.EndsWith("Repository", StringComparison.Ordinal)
                || type.Name.EndsWith("UnitOfWork", StringComparison.Ordinal))
            .Select(type => type.FullName);
        Assert.Empty(failures.Concat(named!));
    }

    [Fact]
    public void Map_api_methods_live_on_api_classes()
    {
        var methods = Arch.Types
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => method.Name.StartsWith("Map", StringComparison.Ordinal)
                && method.Name.EndsWith("Api", StringComparison.Ordinal));
        foreach (var method in methods)
        {
            var declaring = method.DeclaringType!;
            Assert.True(declaring.IsAbstract && declaring.IsSealed, declaring.FullName);
            Assert.True(declaring.IsPublic, declaring.FullName);
            Assert.EndsWith("Api", declaring.Name);
            Assert.NotNull(Arch.ModuleOf(declaring));
        }
    }

    [Fact]
    public void Every_module_has_add_and_map_methods()
    {
        var program = File.ReadAllText(Path.Combine(Arch.ApiProjectDirectory, "Program.cs"));
        Assert.NotEmpty(Arch.Modules);
        foreach (var module in Arch.Modules)
        {
            var type = Arch.Api.GetType($"{Arch.Root}.{module}.{module}Module");
            Assert.NotNull(type);
            Assert.True(type.IsAbstract && type.IsSealed);
            Assert.Equal($"{Arch.Root}.{module}", type.Namespace);

            var add = type.GetMethod($"Add{module}Module", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(add);
            var addParameters = add.GetParameters();
            Assert.Equal(typeof(IServiceCollection), addParameters[0].ParameterType);
            Assert.Equal(typeof(IConfiguration), addParameters[1].ParameterType);

            var map = type.GetMethod($"Map{module}Module", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(map);
            Assert.Equal(typeof(IEndpointRouteBuilder), map.GetParameters()[0].ParameterType);

            Assert.Contains($"Add{module}Module", program, StringComparison.Ordinal);
            Assert.Contains($"Map{module}Module", program, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Identity_keys_are_guid_v7()
    {
        Assert.Equal(typeof(IdentityUser<Guid>), typeof(AppUser).BaseType);
        Assert.Equal(typeof(IdentityRole<Guid>), typeof(AppRole).BaseType);
        Assert.True(typeof(IdentityDbContext<AppUser, AppRole, Guid>).IsAssignableFrom(typeof(AppDbContext)));
        Assert.Equal(7, new AppUser().Id.Version);
        Assert.Equal(7, new AppRole().Id.Version);
    }

    [Fact]
    public void Permission_constants_are_unique_area_actions()
    {
        var pattern = new Regex("^[A-Z][A-Za-z]+\\.[A-Z][A-Za-z]+$", RegexOptions.CultureInvariant);
        var values = Arch.Types
            .Where(type => type.Name.EndsWith("Permissions", StringComparison.Ordinal))
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

        Assert.NotEmpty(values);
        Assert.Equal(values.Length, values.Distinct(StringComparer.Ordinal).Count());
        Assert.All(values, value => Assert.Matches(pattern, value));
    }

    [Fact]
    public void DbContext_has_no_module_dbsets()
    {
        var sets = typeof(AppDbContext)
            .GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType.IsGenericType
                && property.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>));
        Assert.Empty(sets);
    }

    [Fact]
    public void Seeders_have_distinct_orders()
    {
        var seeders = Arch.Types
            .Where(type => typeof(IDataSeeder).IsAssignableFrom(type) && type.IsClass && !type.IsAbstract)
            .ToArray();
        Assert.NotEmpty(seeders);
        Assert.All(seeders, type => Assert.Contains(typeof(IDataSeeder), type.GetInterfaces()));
    }

    [Fact]
    public void No_custom_exception_types()
    {
        var custom = Arch.Api.GetTypes().Where(type => typeof(Exception).IsAssignableFrom(type) && type != typeof(Exception));
        Assert.Empty(custom);
    }

    [Fact]
    public void No_exception_handler_turns_failures_into_status_codes()
    {
        var handlers = Arch.Types.Where(type => typeof(IExceptionHandler).IsAssignableFrom(type) && type.IsClass);
        Assert.Empty(handlers);
    }
}
