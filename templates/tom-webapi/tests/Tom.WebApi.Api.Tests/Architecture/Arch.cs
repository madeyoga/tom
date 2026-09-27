using System.Reflection;
using System.Runtime.CompilerServices;

namespace Tom.WebApi.Api.Tests.Architecture;

internal static class Arch
{
    public static readonly Assembly Api = typeof(Program).Assembly;
    public const string Root = "Tom.WebApi.Api";
    public static readonly string[] Layers = ["Infrastructure", "Shared", "Data"];

    public static IReadOnlyList<Type> Types { get; } = Api.GetTypes()
        .Where(type => type.Namespace?.StartsWith(Root, StringComparison.Ordinal) == true)
        .Where(type => !type.IsDefined(typeof(CompilerGeneratedAttribute), false))
        .ToArray();

    public static string? FeatureOf(Type type)
    {
        if (type.Namespace?.StartsWith(Root + ".", StringComparison.Ordinal) != true)
        {
            return null;
        }

        var parts = type.Namespace.Split('.');
        var depth = Root.Split('.').Length;
        return parts.Length > depth && !Layers.Contains(parts[depth]) ? parts[depth] : null;
    }

    public static IReadOnlyList<string> Features { get; } =
        Types.Select(FeatureOf).OfType<string>().Distinct().Order().ToArray();

    public static string RepoRoot { get; } = FindRepoRoot();

    public static string ApiProjectDirectory { get; } = Path.Combine(RepoRoot, "src", Root);

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
