using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Tom.WebApi.Api.Data;

namespace Tom.WebApi.Api.Tests.Data;

public sealed class TypedIdCompileTests
{
    [Fact]
    public void Passing_NoteId_where_NoteId_is_required_compiles()
    {
        var errors = Compile("""
            using Tom.WebApi.Api.Data;

            static class Probe
            {
                static void Take(NoteId id) { }
                static void Call() => Take(NoteId.New());
            }
            """);

        Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors));
    }

    [Fact]
    public void Passing_a_different_id_type_where_NoteId_is_required_fails_with_CS1503()
    {
        var errors = Compile("""
            using System;
            using Tom.WebApi.Api.Data;

            readonly record struct OrderId(Guid Value);

            static class Probe
            {
                static void Take(NoteId id) { }
                static void Call(OrderId id) => Take(id);
            }
            """);

        Assert.Equal("CS1503", Assert.Single(errors).Id);
    }

    private static Diagnostic[] Compile(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TypedIdProbe",
            [tree],
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
    }

    private static IEnumerable<MetadataReference> References()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            paths.Add(path);
        }

        var api = typeof(NoteId).Assembly;
        paths.Add(api.Location);
        foreach (var reference in api.GetReferencedAssemblies())
        {
            var loaded = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == reference.Name);
            if (!string.IsNullOrEmpty(loaded?.Location))
            {
                paths.Add(loaded.Location);
            }
        }

        return paths.Select(path => MetadataReference.CreateFromFile(path));
    }
}
