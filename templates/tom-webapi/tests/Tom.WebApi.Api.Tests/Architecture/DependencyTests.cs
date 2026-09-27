using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using NetArchTest.Rules;

namespace Tom.WebApi.Api.Tests.Architecture;

public sealed class DependencyTests
{
    // NetArchTest does not see IServiceProvider when it only appears as a property return type
    // (scope.ServiceProvider) or as the parameter of an extension call (GetRequiredService<T>),
    // so the locator entry points are listed too.
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
    public void Lower_layers_do_not_depend_on_features()
    {
        string[] features = [.. Arch.Features.Select(feature => $"{Arch.Root}.{feature}")];
        (string Layer, string[] Forbidden)[] rules =
        [
            ("Data", [.. features, $"{Arch.Root}.Infrastructure"]),
            ("Infrastructure", features),
            ("Shared", [.. features, $"{Arch.Root}.Data", $"{Arch.Root}.Infrastructure"]),
        ];

        var failures = rules
            .SelectMany(rule => Types.InAssembly(Arch.Api)
                .That().ResideInNamespaceMatching($@"^{Regex.Escape($"{Arch.Root}.{rule.Layer}")}(\..+)?$")
                .ShouldNot().HaveDependencyOnAny(rule.Forbidden)
                .GetResult().FailingTypeNames ?? [])
            .ToArray();

        Assert.True(failures.Length == 0, string.Join(", ", failures));
    }

    [Fact]
    public void Business_code_does_not_use_the_service_locator()
    {
        // NetArchTest's ImplementInterface sees direct interfaces only (a BackgroundService subclass
        // does not match), so the exemptions are computed with reflection.
        var exempt = Arch.Types
            .Where(type => typeof(IHostedService).IsAssignableFrom(type)
                || type.Name.EndsWith("Module", StringComparison.Ordinal))
            .Select(type => type.FullName!)
            .ToHashSet();

        var result = Types.InAssembly(Arch.Api)
            .That().ResideInNamespaceMatching($@"^{Regex.Escape(Arch.Root)}\.(?!(Infrastructure|Shared|Data)(\.|$))[^.]+(\..+)?$")
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
