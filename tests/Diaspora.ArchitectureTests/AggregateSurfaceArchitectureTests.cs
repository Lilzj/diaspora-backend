using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// BSOLID-005. A counter is not an invariant, and a published event is not a publication.
/// <para>
/// The finding measured four aggregates over 1,400 lines and recorded that <c>Provider</c> carries
/// 110 public members, "most of them counter deltas". <b>The sizes reproduce exactly and the
/// member count is right; the claim about what they are is not.</b> Provider has 65 public methods
/// of which ten write its statistics object — a sixth of its surface rather than most of it — and the
/// stated impact, that "every counter write loads and saves the whole aggregate, takes its
/// concurrency token, and competes with a genuine edit", does not hold for the aggregate the
/// finding names: <c>Provider</c> has no <c>Revision</c> at all.
/// </para>
/// <para>
/// <b>On the six that do have one, the hazard was already designed out.</b> Not one counter mutator
/// on this platform bumps the revision — the property the finding asks for in its second
/// verification criterion was already true everywhere, not only in Guides where
/// <c>GuideConcurrencyTests.No_engagement_counter_bumps_the_revision_or_raises_an_event</c> asserts
/// it for one module. <see cref="No_counter_mutator_bumps_the_revision"/> is that assertion made
/// platform-wide, so it stays true for the aggregate added next year.
/// </para>
/// <para>
/// <b>What the counters are actually guilty of is being unreachable.</b> Ten of the 51 counter
/// mutators have no caller anywhere in <c>src/</c>, including five of the seven Provider deltas the
/// finding lists as its evidence. They cause no write contention because nothing writes them; six
/// of the figures they maintain are nonetheless published to clients, where they can only ever
/// read zero.
/// </para>
/// <para>
/// <b>The first verification criterion contradicts its own remediation.</b> It asks that no domain
/// aggregate exceed ~800 lines, while step 3 of the remediation beside it says to leave the
/// remaining aggregate size alone because "Provider minus its counters is a large but legitimate
/// aggregate". Provider minus all twelve of its counter mutators is about 1,980 lines.
/// <see cref="The_aggregates_over_the_ceiling_are_the_ones_recorded"/> is the achievable form: the
/// fifteen are recorded exactly, so the set cannot grow and a shrink has to be deliberate.
/// </para>
/// </summary>
public sealed class AggregateSurfaceArchitectureTests
{
    private const int Ceiling = 800;

    /// <summary>
    /// Every aggregate whose file exceeds the finding's ceiling, at its exact length.
    /// <para>
    /// <b>Exact in both directions.</b> A new entry fails, a longer file fails, and so does a
    /// shorter one until this list is edited — which is how a reduction gets recorded rather than
    /// merely happening, and how growth is stopped without pretending 800 is reachable.
    /// </para>
    /// </summary>
    private static readonly (string Aggregate, int Lines)[] AggregatesOverTheCeiling =
    [
        ("Provider", 2044),
        ("MarketplaceListing", 1876),
        ("JobPosting", 1742),
        ("CommunityEvent", 1538),
        ("HousingListing", 1501),
        ("Guide", 1409),
        ("Organization", 1297),
        ("OfficialNotice", 1209),
        ("ModerationItem", 1042),
        ("Campaign", 971),
        ("User", 912),
        ("CandidateProfile", 906),
        ("CommunityPost", 851),
        ("EventSeries", 837),
        ("Notification", 833),
    ];

    /// <summary>
    /// The counter mutators that nothing in <c>src/</c> calls.
    /// <para>
    /// Recorded rather than deleted, because deleting the mutator is the easy half: six of the
    /// figures these maintain — a provider's contacts, impressions, bookmarks, shares, followers
    /// and posts — are mapped into responses clients already receive, so removing the write side
    /// alone would leave a field on the wire that is permanently zero, and removing the field is a
    /// contract change rather than a refactor. The list stops an eleventh appearing.
    /// </para>
    /// </summary>
    private static readonly (string Aggregate, string Mutator)[] CounterMutatorsWithNoCaller =
    [
        ("Provider", "RecordContact"),
        ("Provider", "RecordImpressions"),
        ("Provider", "ApplyBookmarkDelta"),
        ("Provider", "ApplyShareDelta"),
        ("Provider", "ApplyFollowerDelta"),
        ("Provider", "ApplyPostDelta"),
        ("MarketplaceListing", "RecordImpressions"),
        ("JobPosting", "RecordImpressions"),
        ("HousingListing", "RecordEnquiry"),
        ("Guide", "ApplyBookmarkDelta"),
    ];

    // ── Size, recorded rather than wished away ───────────────────────────

    /// <summary>
    /// The aggregates over the ceiling are exactly the ones recorded, at exactly those lengths.
    /// </summary>
    [Fact]
    public void The_aggregates_over_the_ceiling_are_the_ones_recorded()
    {
        var found = Aggregates()
            .Select(pair => (pair.Key, Lines(pair.Value.File)))
            .Where(pair => pair.Item2 > Ceiling)
            .OrderByDescending(pair => pair.Item2)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .ToList();

        found.Should().Equal(
            AggregatesOverTheCeiling,
            "an aggregate's size is recorded so it cannot grow quietly and a reduction cannot " +
            "land without this list saying so. Found: {0}",
            string.Join(", ", found.Select(pair => $"{pair.Item1}={pair.Item2}")));
    }

    /// <summary>
    /// The sanity check every source-reading rule needs: a scan that found no aggregates would
    /// satisfy the rules below only because its own population was empty.
    /// </summary>
    [Fact]
    public void The_scan_read_the_platforms_aggregates()
    {
        Aggregates().Should().HaveCountGreaterThan(
            200, "the platform declares an aggregate root per consistency boundary and there are " +
                 "many; finding few means this scan is reading the wrong thing");
    }

    // ── A counter is not an invariant ────────────────────────────────────

    /// <summary>
    /// <b>The finding's second verification criterion, as a standing rule.</b> No counter-shaped
    /// mutator bumps the aggregate's concurrency token.
    /// <para>
    /// A view, a bookmark or a share is a fact about other people's behaviour, not about the thing
    /// itself. If recording one moved the revision, a popular listing would become unsaveable by
    /// its own owner — the hazard <c>RecordGuideViewCommand</c> names in so many words: "a reader
    /// must never be able to make an author's in-flight save fail."
    /// </para>
    /// <para>
    /// This was already true everywhere when the finding was written, which is worth knowing: the
    /// remediation's first step, moving the counters off the aggregates onto an engagement entity,
    /// buys contention relief that had already been bought a cheaper way.
    /// </para>
    /// </summary>
    [Fact]
    public void No_counter_mutator_bumps_the_revision()
    {
        var offenders = new List<string>();
        var inspected = 0;

        foreach (var (name, (file, declaration)) in Aggregates())
        {
            if (!declaration.Members.OfType<PropertyDeclarationSyntax>()
                    .Any(property => property.Identifier.ValueText == "Revision"))
            {
                continue;
            }

            foreach (var method in CounterMutators(declaration))
            {
                inspected++;

                var touches = method.DescendantNodes()
                    .OfType<IdentifierNameSyntax>()
                    .Any(identifier => identifier.Identifier.ValueText is "Touch" or "Revision");

                if (touches)
                {
                    offenders.Add($"{file}: {name}.{method.Identifier.ValueText}");
                }
            }
        }

        inspected.Should().BeGreaterThan(
            25, "the aggregates that carry an engagement object declare many counter mutators; " +
                "finding few means this rule is reading the wrong thing");

        offenders.Should().BeEmpty(
            "a counter is a fact about other people's behaviour, not about the aggregate, and " +
            "bumping the revision for one makes a popular thing unsaveable by its owner. " +
            "Offenders: {0}",
            string.Join(" | ", offenders));
    }

    /// <summary>
    /// The counter mutators nothing calls are exactly the ones recorded.
    /// </summary>
    [Fact]
    public void The_counter_mutators_without_a_caller_are_the_ones_recorded()
    {
        // Both call shapes, because the second is the one a MemberAccess-only scan misses:
        // `_events?.RecordRsvp(…)` is a MemberBindingExpression, and three of these counters are
        // only ever called through a conditional access.
        var callers = SourceTree.ProductionFiles.ToDictionary(
            file => file.Path,
            file => file.Root.DescendantNodes()
                .Select(node => node switch
                {
                    MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
                    MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
                    _ => null
                })
                .Where(name => name is not null)
                .ToHashSet(StringComparer.Ordinal)!);

        var found = new List<(string, string)>();
        foreach (var (name, (file, declaration)) in Aggregates())
        {
            foreach (var method in CounterMutators(declaration))
            {
                var member = method.Identifier.ValueText;
                var called = callers.Any(pair => pair.Key != file && pair.Value.Contains(member));

                if (!called)
                {
                    found.Add((name, member));
                }
            }
        }

        found.Should().BeEquivalentTo(
            CounterMutatorsWithNoCaller,
            "a counter nothing writes is a figure that can only read zero, and a thirteenth " +
            "would mean a new one was added with no producer. Found: {0}",
            string.Join(", ", found.Select(pair => $"{pair.Item1}.{pair.Item2}")));
    }

    // ── A published event is not a publication ───────────────────────────

    /// <summary>
    /// <b>The enforceable form of the finding's third verification criterion.</b> Every domain
    /// event a relay knows how to translate is reached by a handler.
    /// <para>
    /// The criterion asks that "a new integration event is a new file rather than an edit to a
    /// shared relay". The handlers already are one file per event; what is shared is the
    /// translation, and the relay's own summary argues that is the point — "doing the translation
    /// in a single class means the two never drift". What the criterion is really protecting
    /// against is a link in that chain going missing, and one had:
    /// <c>JobIntegrationEventRelay</c> could translate <c>JobPostingDeletedDomainEvent</c>,
    /// <c>JobPosting.MarkDeleted</c> raised it deliberately so that "downstream de-indexing" could
    /// happen, and no handler existed to carry it from one to the other.
    /// </para>
    /// <para>
    /// Three breaks hide each other here, which is why nothing went red: an aggregate that raises
    /// an event nobody handles commits normally, a public relay method with no caller compiles and
    /// is not dead to any analyzer, and a published contract with no consumer looks exactly like
    /// one whose consumers live in another module.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_relay_overload_is_reachable_from_a_handler()
    {
        var handled = SourceTree.ProductionFiles
            .SelectMany(file => file.Root.DescendantNodes().OfType<GenericNameSyntax>())
            .Where(generic => generic.Identifier.ValueText
                is "DomainEventHandler" or "DomainEventNotification")
            .SelectMany(generic => generic.TypeArgumentList.Arguments)
            .Select(argument => argument.ToString())
            .ToHashSet(StringComparer.Ordinal);

        var overloads = 0;
        var orphans = new List<string>();

        foreach (var file in SourceTree.ProductionFiles)
        {
            foreach (var declaration in file.Root.DescendantNodes()
                         .OfType<ClassDeclarationSyntax>()
                         .Where(type => type.Identifier.ValueText.EndsWith("Relay", StringComparison.Ordinal)))
            {
                foreach (var method in declaration.Members.OfType<MethodDeclarationSyntax>()
                             .Where(method => method.Identifier.ValueText == "PublishAsync"))
                {
                    var parameter = method.ParameterList.Parameters.FirstOrDefault()?.Type?.ToString();

                    if (parameter is null
                        || !parameter.EndsWith("DomainEvent", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    overloads++;
                    var name = parameter.Split('.')[^1];

                    if (!handled.Contains(name))
                    {
                        orphans.Add($"{file.Path}: PublishAsync({name}) has no handler");
                    }
                }
            }
        }

        overloads.Should().BeGreaterThan(
            100, "the platform's relays translate many domain events; finding few means this rule " +
                 "is reading the wrong thing rather than that the relays are small");

        orphans.Should().BeEmpty(
            "a relay overload with no handler is a lifecycle stage the platform believes it " +
            "publishes and does not. Offenders: {0}",
            string.Join(" | ", orphans));
    }

    // ── Reading the source ───────────────────────────────────────────────

    /// <summary>Every class deriving <c>AggregateRoot&lt;…&gt;</c>, by name.</summary>
    private static Dictionary<string, (string File, ClassDeclarationSyntax Declaration)> Aggregates()
    {
        var found = new Dictionary<string, (string, ClassDeclarationSyntax)>(StringComparer.Ordinal);

        foreach (var file in SourceTree.ProductionFiles)
        {
            foreach (var declaration in file.Root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var derives = declaration.BaseList?.Types
                    .Any(type => type.Type is GenericNameSyntax
                    {
                        Identifier.ValueText: "AggregateRoot"
                    }) == true;

                if (derives)
                {
                    found[declaration.Identifier.ValueText] = (file.Path, declaration);
                }
            }
        }

        return found;
    }

    /// <summary>
    /// A counter mutator: a public <c>void</c> method that writes the aggregate's engagement or
    /// statistics value object.
    /// <para>
    /// <b>Defined by what it writes, not by what it is called.</b> A name heuristic over
    /// <c>Apply|Record|Recompute</c> reads <c>ContributionFund.RecordContribution</c> and
    /// <c>AutomationDefinition.RecordDeletion</c> as counters, and those are genuine state changes
    /// that <em>should</em> move the revision. What the finding means by a denormalised counter is
    /// exactly this: a projection of other people's activity, living in a value object beside the
    /// aggregate's own facts. Nine aggregates carry one and declare 51 such mutators between them;
    /// defining it this way needs no exemption list, which a name heuristic did.
    /// </para>
    /// </summary>
    private static IEnumerable<MethodDeclarationSyntax> CounterMutators(
        ClassDeclarationSyntax declaration)
    {
        var counters = declaration.Members
            .OfType<PropertyDeclarationSyntax>()
            .Where(property => property.Type.ToString().EndsWith("Engagement", StringComparison.Ordinal)
                || property.Type.ToString().EndsWith("Statistics", StringComparison.Ordinal))
            .Select(property => property.Identifier.ValueText)
            .ToHashSet(StringComparer.Ordinal);

        if (counters.Count == 0)
        {
            return [];
        }

        return declaration.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(method => method.Modifiers.Any(token => token.ValueText == "public"))
            .Where(method => method.ReturnType is PredefinedTypeSyntax { Keyword.ValueText: "void" })
            .Where(method => method.DescendantNodes()
                .OfType<AssignmentExpressionSyntax>()
                .Any(assignment => assignment.Left is IdentifierNameSyntax identifier
                    && counters.Contains(identifier.Identifier.ValueText)));
    }

    private static int Lines(string path) =>
        File.ReadAllLines(Path.Combine(SourceTree.Root.FullName, path)).Length;
}
