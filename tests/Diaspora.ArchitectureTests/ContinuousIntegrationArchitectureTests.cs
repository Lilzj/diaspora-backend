using FluentAssertions;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// What CI actually runs.
/// <para>
/// <b>A test suite CI does not run is a guardrail whose proof is wired to nothing.</b> Four of the
/// five test projects were named in <c>ci.yml</c> and <c>Diaspora.AnalyzerTests</c> was not — long
/// enough that the analyzer pack grew to twelve rules while its 67 tests never ran on a pull
/// request. The step is there now; this rule is what keeps it there, and what notices the sixth
/// project.
/// </para>
/// <para>
/// <b>The gap bit in one direction only, which is why nothing caught it.</b> A rule that fires
/// wrongly already reddens every build, because the pack's diagnostics are warnings and this
/// repository treats warnings as errors — so a bad rule announces itself everywhere at once. A
/// rule that <i>stops</i> firing is silent: delete a registration and the Release build stays
/// green, every suite passes, and the only thing that would object is the suite CI skipped.
/// Absence of a diagnostic looks exactly like compliance.
/// </para>
/// <para>
/// <b>Read as text, not as YAML.</b> This project references no YAML parser, and what is being
/// asserted is whether a path is handed to <c>dotnet test</c> — a shell string inside the step
/// whatever the structure around it. Comment lines are dropped first, so a project named only in a
/// comment cannot satisfy the rule. No comment in the workflow names a project today; the
/// exclusion is there because every step in that file carries a long explanatory comment, and a
/// commented-out invocation is the obvious way one would arrive. Proven by injecting one and
/// confirming this rule ignored it.
/// </para>
/// </summary>
public sealed class ContinuousIntegrationArchitectureTests
{
    private const string Workflow = ".github/workflows/ci.yml";

    private const string Invocation = "dotnet test ";

    /// <summary>
    /// <b>Every test project in the repository is run by CI.</b>
    /// </summary>
    /// <remarks>
    /// Equality rather than "is a subset", so this also fails when the workflow names a project
    /// that no longer exists. CI would fail by itself in that case, but it would fail with a
    /// restore error partway through a twenty-five-minute run rather than here in milliseconds,
    /// and the message would be about a missing file rather than about the rule.
    /// </remarks>
    [Fact]
    public void Every_test_project_in_the_repository_is_run_by_CI()
    {
        ProjectsInTheRepository().Should().BeEquivalentTo(
            ProjectsCiRuns(),
            "a test project CI does not run proves nothing on a pull request, and the way to "
            + "notice a sixth one being added is for this to fail");
    }

    /// <summary>Every <c>.csproj</c> under <c>tests/</c>, repository-relative with forward slashes.</summary>
    private static List<string> ProjectsInTheRepository()
    {
        var root = SourceTree.Root.FullName;

        return
        [
            .. Directory
                .EnumerateFiles(Path.Combine(root, "tests"), "*.csproj", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                .Where(path => !path.Contains("/bin/", StringComparison.Ordinal)
                    && !path.Contains("/obj/", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
        ];
    }

    /// <summary>Every project handed to <c>dotnet test</c> by the workflow, comments excluded.</summary>
    private static List<string> ProjectsCiRuns()
    {
        var found = new List<string>();

        foreach (var line in File.ReadAllLines(Path.Combine(SourceTree.Root.FullName, Workflow)))
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith('#'))
            {
                continue;
            }

            var index = trimmed.IndexOf(Invocation, StringComparison.Ordinal);

            if (index < 0)
            {
                continue;
            }

            var project = trimmed[(index + Invocation.Length)..].Split(' ')[0].Replace('\\', '/');

            if (project.EndsWith(".csproj", StringComparison.Ordinal))
            {
                found.Add(project);
            }
        }

        return [.. found.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }
}
