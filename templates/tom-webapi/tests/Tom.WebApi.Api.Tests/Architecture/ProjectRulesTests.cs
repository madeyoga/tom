using System.Text.Json;

namespace Tom.WebApi.Api.Tests.Architecture;

public sealed class ProjectRulesTests
{
    [Fact]
    public void Solution_has_one_web_project()
    {
        var projects = ProjectFiles();
        var web = projects.Where(path => File.ReadAllText(path).Contains("Sdk=\"Microsoft.NET.Sdk.Web\"", StringComparison.Ordinal)).ToArray();
        Assert.Single(web);
        var text = File.ReadAllText(web[0]);
        Assert.Contains("net10.0", text, StringComparison.Ordinal);
        Assert.Contains("<Nullable>enable</Nullable>", text, StringComparison.Ordinal);
        Assert.Contains("<ImplicitUsings>enable</ImplicitUsings>", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_folders_match_the_layout()
    {
        var apiDir = Arch.ApiProjectDirectory;
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "Infrastructure", "Shared", "Migrations", "Properties", "bin", "obj", "keys", "emails",
        };
        foreach (var dir in Directory.GetDirectories(apiDir))
        {
            var name = Path.GetFileName(dir);
            if (allowed.Contains(name))
            {
                continue;
            }

            Assert.True(
                File.Exists(Path.Combine(dir, $"{name}Module.cs")),
                $"Top-level folder '{name}' is not a module and is not part of the layout.");
        }

        var forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Controllers", "DTOs", "Dtos", "Interfaces", "Repositories", "Helpers",
        };
        foreach (var dir in Directory.EnumerateDirectories(apiDir, "*", SearchOption.AllDirectories))
        {
            if (dir.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || dir.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var name = Path.GetFileName(dir);

            Assert.False(forbidden.Contains(name), $"Forbidden folder '{dir}'.");
            if (name is "Models" or "Apis" or "Services")
            {
                var parent = Directory.GetParent(dir)!;
                Assert.Equal(apiDir, parent.Parent!.FullName);
                Assert.True(File.Exists(Path.Combine(parent.FullName, $"{parent.Name}Module.cs")));
            }
        }
    }

    [Fact]
    public void Open_api_is_not_enabled_in_appsettings()
    {
        foreach (var name in new[] { "appsettings.json", "appsettings.Production.json" })
        {
            var path = Path.Combine(Arch.ApiProjectDirectory, name);
            if (!File.Exists(path))
            {
                continue;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.TryGetProperty("OpenApi", out var openApi)
                && openApi.TryGetProperty("Enabled", out var enabled))
            {
                Assert.False(enabled.ValueKind == JsonValueKind.True);
            }
        }

        var program = File.ReadAllText(Path.Combine(Arch.ApiProjectDirectory, "Program.cs"));
        var condition = program.IndexOf("OpenApi:Enabled", StringComparison.Ordinal);
        var map = program.IndexOf("MapOpenApi", StringComparison.Ordinal);
        Assert.True(condition >= 0 && map > condition);
        Assert.Equal(map, program.LastIndexOf("MapOpenApi", StringComparison.Ordinal));
        Assert.Contains("IsDevelopment()", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Packages_match_the_stack()
    {
        var projects = ProjectFiles();
        var web = File.ReadAllText(projects.Single(path => File.ReadAllText(path).Contains("Sdk=\"Microsoft.NET.Sdk.Web\"", StringComparison.Ordinal)));
        Assert.DoesNotContain("Swashbuckle", web, StringComparison.Ordinal);
        Assert.DoesNotContain("MediatR", web, StringComparison.Ordinal);
        Assert.DoesNotContain("AutoMapper", web, StringComparison.Ordinal);
        Assert.DoesNotContain("FluentValidation", web, StringComparison.Ordinal);
        Assert.Contains("Microsoft.AspNetCore.OpenApi", web, StringComparison.Ordinal);
        Assert.Contains("Scalar.AspNetCore", web, StringComparison.Ordinal);

        var test = File.ReadAllText(projects.Single(path => path.Contains(".Tests", StringComparison.Ordinal)));
        Assert.Contains("Testcontainers.PostgreSql", test, StringComparison.Ordinal);
        Assert.Contains("Microsoft.AspNetCore.Mvc.Testing", test, StringComparison.Ordinal);
        Assert.DoesNotContain("EntityFrameworkCore.InMemory", test, StringComparison.Ordinal);
        Assert.DoesNotContain("EntityFrameworkCore.Sqlite", test, StringComparison.Ordinal);
    }

    [Fact]
    public void Host_validates_the_service_provider()
    {
        var program = File.ReadAllText(Path.Combine(Arch.ApiProjectDirectory, "Program.cs"));
        Assert.Contains("ValidateScopes = true", program, StringComparison.Ordinal);
        Assert.Contains("ValidateOnBuild = true", program, StringComparison.Ordinal);
    }

    private static string[] ProjectFiles()
        => Directory.GetFiles(Arch.RepoRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();
}
