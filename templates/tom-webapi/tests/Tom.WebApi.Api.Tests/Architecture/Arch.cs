using System.Reflection;
using System.Runtime.CompilerServices;

namespace Tom.WebApi.Api.Tests.Architecture;

internal static class Arch
{
    public static readonly Assembly Api = typeof(Program).Assembly;
    public const string Root = "Tom.WebApi.Api";
    private static readonly string[] NonModules = ["Infrastructure", "Shared", "Migrations"];

    public static IReadOnlyList<Type> Types { get; } = Api.GetTypes()
        .Where(type => type.Namespace?.StartsWith(Root, StringComparison.Ordinal) == true)
        .Where(type => !type.IsDefined(typeof(CompilerGeneratedAttribute), false))
        .ToArray();

    public static string? ModuleOf(Type type)
    {
        if (type.Namespace?.StartsWith(Root + ".", StringComparison.Ordinal) != true)
        {
            return null;
        }

        var parts = type.Namespace.Split('.');
        var depth = Root.Split('.').Length;
        return parts.Length > depth && !NonModules.Contains(parts[depth]) ? parts[depth] : null;
    }

    public static IReadOnlyList<string> Modules { get; } =
        Types.Select(ModuleOf).OfType<string>().Distinct().Order().ToArray();

    public static bool InNamespace(Type type, string ns)
        => type.Namespace is { } name && (name == ns || name.StartsWith(ns + ".", StringComparison.Ordinal));

    public static bool InModule(Type type, string module) => InNamespace(type, $"{Root}.{module}");

    public static bool IsContracts(Type type)
        => ModuleOf(type) is { } module && InNamespace(type, $"{Root}.{module}.Contracts");

    public static string RepoRoot { get; } = FindRepoRoot();

    public static string ApiProjectDirectory { get; } = Path.Combine(RepoRoot, "src", $"{Root}");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("*.sln").Length > 0 || dir.GetFiles("*.slnx").Length > 0)
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Solution file not found.");
    }
}
