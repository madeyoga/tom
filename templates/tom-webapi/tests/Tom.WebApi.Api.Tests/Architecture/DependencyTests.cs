using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using NetArchTest.Rules;

namespace Tom.WebApi.Api.Tests.Architecture;

public sealed class DependencyTests
{
    private static readonly string[] Locator =
    [
        "System.IServiceProvider",
        "Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions",
        "Microsoft.Extensions.DependencyInjection.ServiceProviderKeyedServiceExtensions",
        "Microsoft.Extensions.DependencyInjection.IServiceScopeFactory",
        "Microsoft.Extensions.DependencyInjection.IServiceScope",
        "Microsoft.Extensions.DependencyInjection.AsyncServiceScope",
    ];

    [Fact]
    public void Modules_use_only_other_modules_contracts()
    {
        var failures = new List<string>();
        foreach (var a in Arch.Modules)
        {
            foreach (var b in Arch.Modules.Where(module => module != a))
            {
                var forbidden = Arch.Types
                    .Where(type => Arch.InModule(type, b) && !Arch.IsContracts(type) && !type.IsNested)
                    .Select(type => type.FullName!)
                    .ToArray();
                if (forbidden.Length == 0)
                {
                    continue;
                }

                var result = Types.InAssembly(Arch.Api)
                    .That().ResideInNamespaceMatching($@"^{Regex.Escape(Arch.Root)}\.{Regex.Escape(a)}(\..+)?$")
                    .And().DoNotHaveNameEndingWith("Configuration")
                    .ShouldNot().HaveDependencyOnAny(forbidden)
                    .GetResult();

                if (!result.IsSuccessful)
                {
                    failures.AddRange(result.FailingTypeNames.Select(name => $"{name} -> {b} internals"));
                }
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void Shared_does_not_depend_on_modules()
    {
        var modules = Arch.Modules.Select(module => $"{Arch.Root}.{module}").ToArray();
        if (modules.Length == 0)
        {
            return;
        }

        var result = Types.InAssembly(Arch.Api)
            .That().ResideInNamespaceMatching($@"^{Regex.Escape(Arch.Root)}\.Shared(\..+)?$")
            .ShouldNot().HaveDependencyOnAny(modules)
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Business_code_does_not_use_the_service_locator()
    {
        var exempt = Arch.Types
            .Where(type => typeof(IHostedService).IsAssignableFrom(type)
                || type.Name.EndsWith("Module", StringComparison.Ordinal))
            .Select(type => type.FullName!)
            .ToHashSet();

        var result = Types.InAssembly(Arch.Api)
            .That().ResideInNamespaceMatching($@"^{Regex.Escape(Arch.Root)}\.(?!(Infrastructure|Shared|Migrations)(\.|$))[^.]+(\..+)?$")
            .ShouldNot().HaveDependencyOnAny(Locator)
            .GetResult();

        var failures = (result.FailingTypeNames ?? []).Where(name => !exempt.Contains(name)).ToArray();
        Assert.True(failures.Length == 0, string.Join(", ", failures));
    }

    [Fact]
    public void No_mvc_controllers()
    {
        var controllers = Arch.Types.Where(type => typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(type));
        Assert.Empty(controllers);
    }
}
