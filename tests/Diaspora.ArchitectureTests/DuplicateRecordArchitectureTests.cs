using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// BBL-002 and BSOLID-006. One place asks whether a failure was a unique-index violation, and one
/// place reads the <c>Idempotency-Key</c> header.
/// <para>
/// <b>These rules exist because the ones that were supposed to cover this did not.</b> Every module
/// has a test asserting that its <em>Domain</em> namespace does not depend on EF Core, and
/// <c>ConcurrencyConflictException</c>'s own documentation claims "the application layer is not
/// allowed to reference EF Core — an architecture test enforces that". No rule covered a module's
/// <em>Application</em> namespace, so three modules kept a private copy of one message-matching
/// predicate there, each catching <c>DbUpdateException</c> in the layer that was documented as
/// unable to see it. Nothing failed, and 232 of the platform's 234 unique indexes answered a
/// concurrent duplicate with a 500.
/// </para>
/// <para>
/// A blanket "no module Application namespace may reference EF Core" rule is not written here, and
/// deliberately: one file legitimately does
/// (<c>Financial/Application/Campaigns/CampaignAnalyticsSource.cs</c>, a read-side query using
/// <c>AsNoTracking</c> and <c>ToListAsync</c>). A rule carrying an allowlist of one invites a
/// second entry. The rules below name the actual mistake instead.
/// </para>
/// </summary>
public sealed class DuplicateRecordArchitectureTests
{
    private const string PersistenceProject = "src/Diaspora.Persistence/";

    /// <summary>
    /// The sanity check every source-reading rule depends on: a scan that matches nothing passes
    /// for the worst possible reason.
    /// </summary>
    [Fact]
    public void The_source_tree_is_actually_being_read()
    {
        SourceTree.ProductionFiles.Should().HaveCountGreaterThan(1_000);

        SourceTree.ProductionFiles
            .Select(file => file.Path)
            .Should().Contain(path => path.StartsWith(PersistenceProject, StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>Only persistence catches EF Core's save failure.</b> Everywhere else catches
    /// <c>DuplicateRecordException</c>, which is the same fact with the provider taken out of it.
    /// </summary>
    [Fact]
    public void Only_persistence_catches_the_frameworks_save_failure()
    {
        var offenders = SourceTree.ProductionFiles
            .Where(file => !file.Path.StartsWith(PersistenceProject, StringComparison.Ordinal))
            .SelectMany(file => file.Root
                .DescendantNodes()
                .OfType<CatchClauseSyntax>()
                .Where(NamesTheFrameworksSaveFailure)
                .Select(file.Locate))
            .ToList();

        offenders.Should().BeEmpty(
            "a module that catches DbUpdateException is a module re-deriving a unique violation " +
            "from a provider's message. DiasporaDbContext answers that question once and throws " +
            "DuplicateRecordException; catch that instead. Offenders: {0}",
            string.Join(", ", offenders));
    }

    /// <summary>
    /// The predicate itself, by its evidence. A future copy would have to match a provider's
    /// message, and these are the three substrings the two providers put in it — so this catches a
    /// fourth private copy however its author names the method.
    /// </summary>
    [Fact]
    public void Only_persistence_knows_what_a_unique_violation_looks_like()
    {
        string[] fingerprints =
            ["UNIQUE constraint failed", "duplicate key value", "23505"];

        var offenders = SourceTree.ProductionFiles
            .Where(file => !file.Path.StartsWith(PersistenceProject, StringComparison.Ordinal))
            .SelectMany(file => file.Root
                .DescendantNodes()
                .OfType<LiteralExpressionSyntax>()
                .Where(literal => literal.Token.Value is string text
                    && fingerprints.Any(fingerprint =>
                        text.Contains(fingerprint, StringComparison.Ordinal)))
                .Select(file.Locate))
            .ToList();

        offenders.Should().BeEmpty(
            "matching a database provider's error text is persistence's job and nowhere else's. " +
            "There were three identical copies of that predicate, one per module that had " +
            "noticed the problem. Offenders: {0}",
            string.Join(", ", offenders));
    }

    /// <summary>
    /// <b>One owner for a wire contract.</b> There were four readers of this header, and the fourth
    /// was found only because a call site referenced a type no file was named after — it is
    /// declared at the foot of <c>IntegrationsEndpointContributor.cs</c>. So this rule counts
    /// declarations in the syntax rather than files on disk.
    /// </summary>
    [Fact]
    public void One_type_reads_the_idempotency_header()
    {
        var declarations = SourceTree.ProductionFiles
            .SelectMany(file => file.Root
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Where(declaration => declaration.Identifier.ValueText == "IdempotencyHeader")
                .Select(file.Locate))
            .ToList();

        declarations.Should().ContainSingle(
            "four modules each declared one. Found: {0}", string.Join(", ", declarations));

        declarations[0].Should().StartWith(
            "src/Diaspora.Platform/Http/IdempotencyHeader.cs",
            "it belongs beside PlatformProblem and PlatformResults, with the rest of the " +
            "platform's HTTP vocabulary");
    }

    /// <summary>
    /// And no module spells the header inline instead. The payment adapters are exempt because
    /// they <em>send</em> this header to a provider — the same name, the opposite direction, and a
    /// contract owned by Stripe rather than by this platform.
    /// </summary>
    [Fact]
    public void No_module_spells_the_header_for_itself()
    {
        var offenders = SourceTree.ProductionFiles
            .Where(file => file.Path.StartsWith("src/Diaspora.Modules.", StringComparison.Ordinal))
            .SelectMany(file => file.Root
                .DescendantNodes()
                .OfType<LiteralExpressionSyntax>()
                .Where(literal => literal.Token.Value is "Idempotency-Key")
                .Select(file.Locate))
            .ToList();

        offenders.Should().BeEmpty(
            "use IdempotencyHeader.Name. A module that writes the string is a module that can " +
            "misspell it, and the client sends one spelling to every route. Offenders: {0}",
            string.Join(", ", offenders));
    }

    /// <summary>
    /// <b>A rule because no behavioural test can be one.</b> Two things keep a lost update out of
    /// the duplicate arm — this order, and a predicate that matches text EF's concurrency message
    /// does not contain — and they are redundant. Breaking either on its own leaves the suite
    /// green; that was measured, not assumed. So the order is real defensive work with nothing
    /// observing it, and the realistic way to lose it is a later author adding a fourth substring
    /// to the predicate while the arms happen to be the wrong way round.
    /// <para>
    /// <c>DbUpdateConcurrencyException</c> derives from <c>DbUpdateException</c>, so with the arms
    /// reversed a filter that matched would swallow every lost update and tell the caller not to
    /// retry something a retry would have fixed.
    /// </para>
    /// <para>
    /// <b>Anchored on the declaration rather than on a file.</b> <c>PersistAsync</c> left
    /// <c>DiasporaDbContext</c> for <c>SaveAndDispatchCoordinator</c> on 21 November 2026
    /// (BSOLID-002), and a rule keyed on a path would have had to be edited to follow it — which
    /// is the edit somebody makes by deleting the assertion. The count below is the population
    /// guard that goes with that: a scan finding no declaration would otherwise compare an empty
    /// list of catch arms against an empty list and pass.
    /// </para>
    /// </summary>
    [Fact]
    public void The_concurrency_arm_is_tried_before_the_duplicate_arm()
    {
        var declarations = SourceTree.ProductionFiles
            .Where(file => file.Path.StartsWith(PersistenceProject, StringComparison.Ordinal))
            .SelectMany(file => file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Where(method => method.Identifier.ValueText == "PersistAsync")
            .ToList();

        declarations.Should().HaveCount(
            1,
            "the platform has exactly one place that writes the tracker and translates what the " +
            "write threw; a second would be a second opinion on what a duplicate is");

        var arms = declarations[0]
            .DescendantNodes()
            .OfType<CatchClauseSyntax>()
            .Select(clause => clause.Declaration?.Type.ToString() ?? "(no declaration)")
            .ToList();

        arms.Should().Equal(
            ["DbUpdateConcurrencyException", "DbUpdateException"],
            "PersistAsync translates exactly these two families, derived type first");
    }

    private static bool NamesTheFrameworksSaveFailure(CatchClauseSyntax clause)
    {
        var declared = clause.Declaration?.Type.ToString();

        if (declared is not null)
        {
            return declared.EndsWith("DbUpdateException", StringComparison.Ordinal);
        }

        // `catch (Exception e) when (SomethingElse.IsUniqueViolation(e))` declares Exception and
        // hides the real subject in the filter. That was the seventh call site, and a grep for the
        // type name did not find it.
        return clause.Filter is not null
            && clause.Filter.ToString().Contains("UniqueViolation", StringComparison.OrdinalIgnoreCase);
    }
}
