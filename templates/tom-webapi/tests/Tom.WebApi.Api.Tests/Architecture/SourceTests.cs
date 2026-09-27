using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tom.WebApi.Api.Tests.Architecture;

internal sealed record SourceFile(string Path, CompilationUnitSyntax Root)
{
    public string? TopFolder => Path.IndexOf('/') is var index and > 0 ? Path[..index] : null;

    public bool IsGenerated => Path.StartsWith("Data/Migrations/", StringComparison.Ordinal);
}

internal static class Source
{
    public static readonly string RepoRoot = FindRepoRoot();
    public static readonly string ApiDir = System.IO.Path.Combine(RepoRoot, "src", Arch.Root);

    public static IReadOnlyList<SourceFile> ApiFiles { get; } = Parse(ApiDir);

    public static IReadOnlyList<SourceFile> Parse(string dir) => Directory
        .EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)
        .Select(path => System.IO.Path.GetRelativePath(dir, path).Replace('\\', '/'))
        .Where(path => !path.Split('/').Any(segment => segment is "bin" or "obj"))
        .Select(path => new SourceFile(
            path,
            CSharpSyntaxTree.ParseText(File.ReadAllText(System.IO.Path.Combine(dir, path))).GetCompilationUnitRoot()))
        .ToArray();

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (dir.EnumerateFiles("*.sln").Any() || dir.EnumerateFiles("*.slnx").Any())
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("No .sln or .slnx above the test output directory.");
    }
}

internal static class FolderGraph
{
    private static readonly Dictionary<string, HashSet<string>> Declared = Source.ApiFiles
        .Where(file => file.TopFolder is not null)
        .GroupBy(file => file.TopFolder!)
        .ToDictionary(group => group.Key, group => group
            .SelectMany(file => file.Root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            .Select(type => type.Identifier.Text)
            .ToHashSet());

    public static bool IsFeature(string? folder) => folder is not null && !Arch.Layers.Contains(folder);

    // Top-level folders a file uses: qualified names ({Root}.X... in usings, parameters, or calls),
    // plus type names declared only under X/.
    public static IEnumerable<string> DependenciesOf(SourceFile file)
    {
        HashSet<string> own = file.TopFolder is { } top && Declared.TryGetValue(top, out var names) ? names : [];
        var prefix = Arch.Root + ".";
        var qualified = file.Root.DescendantNodes()
            .Where(node => node is QualifiedNameSyntax or MemberAccessExpressionSyntax)
            .Select(node => node.ToString())
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
            .Select(name => name[prefix.Length..].Split('.')[0]);
        var used = file.Root.DescendantNodes().OfType<SimpleNameSyntax>()
            .Where(IsTypePosition)
            .Select(name => name.Identifier.Text)
            .Where(name => !own.Contains(name))
            .ToHashSet();
        var byName = Declared.Where(declared => declared.Value.Overlaps(used)).Select(declared => declared.Key);
        return qualified.Concat(byName).Where(dependency => dependency != file.TopFolder).Distinct();
    }

    // Skip member names: x.Name, { Name = ... } initializers, and Name: arguments.
    private static bool IsTypePosition(SimpleNameSyntax name) => name.Parent switch
    {
        MemberAccessExpressionSyntax member => member.Expression == name,
        NameColonSyntax => false,
        AssignmentExpressionSyntax assignment => !(assignment.Left == name && assignment.Parent is InitializerExpressionSyntax),
        _ => true,
    };
}

public sealed class SourceTests
{
    [Fact]
    public void No_feature_dependency_cycles()
    {
        var edges = Source.ApiFiles
            .Where(file => FolderGraph.IsFeature(file.TopFolder))
            .SelectMany(file => FolderGraph.DependenciesOf(file)
                .Where(FolderGraph.IsFeature)
                .Select(dependency => (From: file.TopFolder!, To: dependency)))
            .Distinct()
            .ToLookup(edge => edge.From, edge => edge.To);

        var cycles = new List<string>();
        var done = new HashSet<string>();
        void Visit(string node, List<string> path)
        {
            if (path.IndexOf(node) is var index and >= 0)
            {
                cycles.Add(string.Join(" -> ", path[index..].Append(node)));
                return;
            }

            if (!done.Add(node))
            {
                return;
            }

            path.Add(node);
            foreach (var next in edges[node])
            {
                Visit(next, path);
            }

            path.RemoveAt(path.Count - 1);
        }

        foreach (var feature in edges.Select(group => group.Key).Order())
        {
            Visit(feature, []);
        }

        Assert.True(cycles.Count == 0, string.Join(Environment.NewLine, cycles));
    }

    // Program.cs and other project-root files compose every feature, so they are skipped.
    [Fact]
    public void Nothing_depends_on_reports()
    {
        var failures = Source.ApiFiles
            .Where(file => file.TopFolder is not null && file.TopFolder != "Reports")
            .Where(file => FolderGraph.DependenciesOf(file).Contains("Reports"))
            .Select(file => file.Path)
            .ToArray();

        Assert.True(failures.Length == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Namespace_matches_top_level_folder()
    {
        var failures = Source.ApiFiles
            .Where(file => file.TopFolder is not null && !file.IsGenerated)
            .SelectMany(file => file.Root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>()
                .Select(ns => ns.Name.ToString())
                .DefaultIfEmpty("(global)")
                .Where(ns => ns != $"{Arch.Root}.{file.TopFolder}")
                .Select(ns => $"{file.Path}: {ns}, expected {Arch.Root}.{file.TopFolder}"))
            .ToArray();

        Assert.True(failures.Length == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Data_files_are_named_after_their_type()
    {
        var failures = new List<string>();
        foreach (var file in Source.ApiFiles.Where(file => file.TopFolder == "Data" && !file.IsGenerated))
        {
            var name = System.IO.Path.GetFileName(file.Path).Split('.')[0];
            var types = file.Root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
                .Where(type => type.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax)
                .ToArray();
            if (!types.Any(type => type.Identifier.Text == name))
            {
                failures.Add($"{file.Path}: declares no type named {name}");
            }

            var configured = types
                .SelectMany(type => type.BaseList is { } list ? list.Types.Select(baseType => baseType.Type) : [])
                .OfType<GenericNameSyntax>()
                .Where(generic => generic.Identifier.Text == "IEntityTypeConfiguration")
                .Select(generic => generic.TypeArgumentList.Arguments[0].ToString());
            failures.AddRange(configured.Where(configuredType => configuredType != name)
                .Select(configuredType => $"{file.Path}: configuration for {configuredType} belongs in {configuredType}.cs"));
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Queries_use_method_syntax()
    {
        var failures = Source.Parse(Source.RepoRoot)
            .SelectMany(file => file.Root.DescendantNodes().OfType<QueryExpressionSyntax>()
                .Select(query => $"{file.Path}:{query.GetLocation().GetLineSpan().StartLinePosition.Line + 1}"))
            .ToArray();

        Assert.True(failures.Length == 0, string.Join(Environment.NewLine, failures));
    }
}
