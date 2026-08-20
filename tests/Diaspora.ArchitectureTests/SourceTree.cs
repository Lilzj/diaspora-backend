using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The platform's own source, parsed.
/// <para>
/// <b>Some rules cannot be checked against compiled metadata, and pretending otherwise is how they
/// go unenforced for three phases.</b> "Do not call <c>ToString()</c> on a strongly typed
/// identifier inside a query predicate" is a statement about the shape of a lambda; reflection sees
/// a compiled expression-tree factory and nothing useful. Roslyn sees the lambda. B1.2 established
/// that this suite may compile source to prove a rule — this is the same technique pointed at code
/// that already exists rather than code that must not.
/// </para>
/// <para>
/// Syntax only, deliberately: no compilation, no semantic model, no project load. It runs in
/// milliseconds and cannot be defeated by a build failure elsewhere. The cost is that the patterns
/// are matched textually and must therefore be narrow enough that a false positive would be
/// strange — which the rules that use it are.
/// </para>
/// </summary>
internal static class SourceTree
{
    private static readonly Lazy<IReadOnlyList<SourceFile>> Files = new(Load);

    /// <summary>Every hand-written source file under <c>src/</c>.</summary>
    public static IReadOnlyList<SourceFile> ProductionFiles => Files.Value;

    /// <summary>The repository root, found by walking up to the solution file.</summary>
    public static DirectoryInfo Root { get; } = FindRoot();

    private static DirectoryInfo FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (directory.GetFiles("Diaspora.slnx").Length > 0)
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not find Diaspora.slnx above " + AppContext.BaseDirectory +
            ". The source-reading architecture rules must run from inside the repository.");
    }

    private static List<SourceFile> Load()
    {
        var source = new DirectoryInfo(Path.Combine(Root.FullName, "src"));

        return source
            .EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(file => !IsGenerated(file))
            .Select(file =>
            {
                var text = File.ReadAllText(file.FullName);

                return new SourceFile(
                    Path.GetRelativePath(Root.FullName, file.FullName).Replace('\\', '/'),
                    CSharpSyntaxTree.ParseText(text).GetRoot());
            })
            .ToList();
    }

    /// <summary>
    /// Build output and EF's scaffolded migrations. Migrations are machine-written and enormous,
    /// and a rule about how humans should write queries has nothing to say about them.
    /// </summary>
    private static bool IsGenerated(FileInfo file)
    {
        var path = file.FullName.Replace('\\', '/');

        return path.Contains("/obj/", StringComparison.Ordinal)
            || path.Contains("/bin/", StringComparison.Ordinal)
            || path.Contains("/Migrations/", StringComparison.Ordinal);
    }
}

/// <summary>One parsed file, and where it came from.</summary>
internal sealed record SourceFile(string Path, SyntaxNode Root)
{
    /// <summary>The line a node sits on, for a failure message somebody can click.</summary>
    public string Locate(SyntaxNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        return $"{Path}:{line}";
    }
}
