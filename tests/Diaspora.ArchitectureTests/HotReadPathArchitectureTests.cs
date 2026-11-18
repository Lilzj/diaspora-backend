using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The shape a read on a hot path is allowed to have.
/// <para>
/// <b>BBL-005 and BARCH-005 are one finding, and its remedy has two halves.</b> The loops are gone
/// — the feed asks Trust once for a page of authors and the conversation list asks for every last
/// message at once — and <c>HotReadQueryBudgetTests</c> holds the repositories to one statement
/// each at any page size. What neither of those does is stop the loop coming back. A budget test
/// measures the repository; this measures the caller, which is where both defects actually lived.
/// </para>
/// <para>
/// <b>Named methods rather than the whole platform, deliberately.</b> The audit's scan for an
/// awaited call inside a <c>foreach</c> returned 77 candidate sites and hand-verified two; most of
/// the remaining 75 are sweeps and administrative paths where sequential work is the correct
/// design, and a rule that failed on all of them would be turned off within a day. The list below
/// is the paths where it is known to be wrong, and it is meant to grow — a request-scoped read
/// added to it is a decision somebody makes once, which is the opposite of 75 silent ones.
/// </para>
/// </summary>
public sealed class HotReadPathArchitectureTests
{
    /// <summary>One method that composes a page, and therefore may not read one row at a time.</summary>
    /// <param name="File">Repository-relative path, as <see cref="SourceTree"/> reports it.</param>
    /// <param name="Method">The method name, which must exist — see the rule below.</param>
    /// <param name="Why">What the loop cost when it was there, so a reader knows the stakes.</param>
    private sealed record PageComposingMethod(string File, string Method, string Why);

    /// <summary>
    /// The read paths a page is composed from.
    /// <para>
    /// Both entries are the sites BBL-005 cites by file and line. They are the platform's two most
    /// used screens, which is why they were the two the audit hand-verified out of 77.
    /// </para>
    /// </summary>
    private static readonly PageComposingMethod[] PageComposingMethods =
    [
        new("src/Diaspora.Modules.Community/Application/Services/FeedComposer.cs",
            "BuildContextAsync",
            "one round trip to Trust per distinct author, on the primary screen"),

        new("src/Diaspora.Modules.Conversations/Application/Queries/ConversationQueries.cs",
            "LoadLastMessagesAsync",
            "one primary-key lookup per thread, bounded at a hundred, on the second-most-used screen")
    ];

    /// <summary>
    /// <b>The rule.</b> A method that composes a page does not await inside a loop.
    /// <para>
    /// Written as a property of the syntax rather than of the port it calls, because the defect is
    /// not which method was called — <c>GetTrustLevelAsync</c> is correct everywhere else — but
    /// that it was called once per row. A future loop over some other single-subject port is the
    /// same defect and this catches it without knowing the port exists.
    /// </para>
    /// </summary>
    [Fact]
    public void A_method_that_composes_a_page_does_not_read_one_row_at_a_time()
    {
        var offenders = new List<string>();

        foreach (var composing in PageComposingMethods)
        {
            var method = Find(composing);

            foreach (var awaited in method.DescendantNodes().OfType<AwaitExpressionSyntax>())
            {
                var loop = awaited.Ancestors()
                    .TakeWhile(ancestor => ancestor != method)
                    .FirstOrDefault(IsALoop);

                if (loop is null)
                {
                    continue;
                }

                offenders.Add(
                    $"{Locate(composing, awaited)}: {composing.Method} awaits inside a " +
                    $"{loop.GetType().Name}. Was: {composing.Why}. Ask the port for the whole " +
                    "page — add " +
                    "a batch form to it if it has none, which is what the finding was about.");
            }
        }

        offenders.Should().BeEmpty(
            "a page is one read per kind of fact, not one per row:\n" + string.Join("\n", offenders));
    }

    /// <summary>
    /// <b>The list names methods that exist, or it guards nothing.</b> A rule keyed on a method name
    /// passes silently the day somebody renames or inlines the method, and this one would then be
    /// asserting that a method with no statements contains no loops.
    /// </summary>
    [Fact]
    public void Every_named_page_composing_method_still_exists()
    {
        PageComposingMethods.Should().HaveCountGreaterThanOrEqualTo(
            2, "both sites the finding cites are listed");

        foreach (var composing in PageComposingMethods)
        {
            var method = Find(composing);

            method.DescendantNodes().OfType<AwaitExpressionSyntax>().Should().NotBeEmpty(
                "{0} composes a page from data it fetches, so it awaits something — a body with " +
                "no await at all means the method moved and this entry is now checking nothing",
                composing.Method);
        }
    }

    /// <summary>
    /// <b>The rule is shown to fail on the code it was written for.</b> Both methods used to
    /// contain exactly the loop above, so the detector is run over that same shape here: an await
    /// inside a <c>foreach</c>, in a method of the same form. Without this, a detector that never
    /// matched anything would read as a clean codebase forever.
    /// </summary>
    [Fact]
    public void The_detector_finds_the_loop_the_finding_was_about()
    {
        const string theLoopAsItWas = """
            namespace Probe;
            internal sealed class Probe
            {
                private async Task<Dictionary<Guid, int>> BuildContextAsync(
                    IEnumerable<Guid> authorIds, CancellationToken cancellationToken)
                {
                    var trustLevels = new Dictionary<Guid, int>();
                    foreach (var authorId in authorIds)
                    {
                        trustLevels[authorId] = (int)await GetTrustLevelAsync(
                            authorId, cancellationToken);
                    }
                    return trustLevels;
                }
            }
            """;

        const string theLoopAsItIsNow = """
            namespace Probe;
            internal sealed class Probe
            {
                private async Task<Dictionary<Guid, int>> BuildContextAsync(
                    IReadOnlyCollection<Guid> authorIds, CancellationToken cancellationToken)
                {
                    var levels = await GetTrustLevelsAsync(authorIds, cancellationToken);
                    foreach (var entry in levels)
                    {
                        Record(entry);
                    }
                    return levels.ToDictionary(e => e.Key, e => (int)e.Value);
                }
            }
            """;

        AwaitsInsideALoop(theLoopAsItWas, "BuildContextAsync").Should().BeTrue(
            "this is the body BBL-005 quotes, and a detector that does not see it sees nothing");

        AwaitsInsideALoop(theLoopAsItIsNow, "BuildContextAsync").Should().BeFalse(
            "one await before a loop is not an await inside one, and a rule that could not tell " +
            "them apart would forbid iterating over a batched result");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static bool IsALoop(SyntaxNode node) =>
        node is ForEachStatementSyntax
            or ForEachVariableStatementSyntax
            or ForStatementSyntax
            or WhileStatementSyntax
            or DoStatementSyntax;

    private static MethodDeclarationSyntax Find(PageComposingMethod composing)
    {
        var file = SourceTree.ProductionFiles.SingleOrDefault(candidate =>
            string.Equals(candidate.Path, composing.File, StringComparison.Ordinal));

        file.Should().NotBeNull(
            "{0} is a path this rule reads, and a rule reading a file that is not there is a rule " +
            "that passes for the wrong reason",
            composing.File);

        var method = file!.Root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .SingleOrDefault(candidate => string.Equals(
                candidate.Identifier.ValueText, composing.Method, StringComparison.Ordinal));

        method.Should().NotBeNull(
            "{0} declares {1}", composing.File, composing.Method);

        return method!;
    }

    private static string Locate(PageComposingMethod composing, SyntaxNode node) =>
        $"{composing.File}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";

    /// <summary>
    /// The same detector the rule uses, over a snippet rather than a file — which is the point:
    /// a probe against a second copy would prove something about the copy.
    /// </summary>
    private static bool AwaitsInsideALoop(string source, string methodName)
    {
        var method = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(source)
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(candidate => string.Equals(
                candidate.Identifier.ValueText, methodName, StringComparison.Ordinal));

        return method.DescendantNodes()
            .OfType<AwaitExpressionSyntax>()
            .Any(awaited => awaited.Ancestors()
                .TakeWhile(ancestor => ancestor != method)
                .Any(IsALoop));
    }
}
