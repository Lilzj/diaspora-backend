using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// Which namespace a permitted module reference is allowed to touch.
/// <para>
/// <b>The sibling guardrail answers a different question.</b>
/// <see cref="ModuleDependencyGuardrailArchitectureTests"/> enforces three rules — the module graph
/// is acyclic by composition order, no module names the host or its adapters, and the two consumer
/// modules reference no vertical at all. All three are about <em>whether</em> module A may reference
/// module B. None of them is about <em>what</em> A may then reach inside B, and a project reference
/// grants every <c>public</c> type in the assembly.
/// </para>
/// <para>
/// <b>So the boundary the platform designed has never been enforced.</b> Each module publishes
/// <c>Contracts</c> (frozen payloads), <c>Extensibility</c> (seams others implement or call) and
/// <c>Integration</c> (its own cross-module adapters), and keeps <c>Domain</c>, <c>Application</c>
/// and <c>Persistence</c> to itself. That convention is declared in ADR-001 and in the sibling
/// guardrail's own summary, both of which describe extracting a module as a configuration change.
/// For the edges listed below it is not: a module whose compilation depends on another's aggregates
/// cannot be extracted without being rewritten first.
/// </para>
/// <para>
/// <b>C# cannot express this one.</b> <c>internal</c> is unavailable because these are separate
/// assemblies and the types are <c>public</c> for the module's own <c>Api</c> and <c>Persistence</c>
/// folders to use. <c>[InternalsVisibleTo]</c> grants more access rather than less, so it inverts
/// the problem. The boundary is a convention about namespaces, which makes a test the only
/// mechanism that can hold it — which is why the audit recommended exactly this and why it is a
/// ratchet rather than a clean assertion: there are 107 crossings today and a rule that simply
/// failed would be deleted by the end of the week.
/// </para>
/// <para>
/// <b>Some of these crossings cannot be closed where they are written, and anyone working the list
/// down should know that before starting.</b> A published seam may itself be typed in its own
/// module's domain, in which case every module that implements or calls it is obliged to import
/// those internals and no change at the call site can avoid it.
/// <c>Financial.Extensibility.Advertising.IAdTargetingOptionProvider</c> is one: its first line is
/// <c>using Diaspora.Modules.Financial.Domain.Advertising;</c> and its members are typed
/// <c>AdTargetingDimension</c> and <c>AdAudience</c>, so
/// <c>CommunityProgramme/Infrastructure/Advertising/CommunityTargetingContributors.cs</c> reaches
/// <c>Financial.Domain</c> because the seam it implements requires it. That one was read; a
/// reflection scan written while this rule was being added put the shape at roughly 236 of 1,833
/// published types, which is recorded as an indication and not as a measurement — it has not been
/// cross-checked against a second, differently written scan, and on this finding the first count
/// was wrong twice. Worth its own change, with its own rule.
/// </para>
/// <para>
/// Raised as BARCH-001 (report 4) and BSOLID-003 (report 2) of the November 2026 pre-launch audit,
/// which are one finding — report 2 says so in its own cross-reference field.
/// </para>
/// </summary>
public sealed class ModuleEncapsulationArchitectureTests
{
    private const string ModulePrefix = "Diaspora.Modules.";
    private const string ModuleSourcePrefix = "src/Diaspora.Modules.";

    /// <summary>What a module publishes. Everything else in it is its own business.</summary>
    private static readonly string[] PublishedSegments = ["Contracts", "Extensibility", "Integration"];

    /// <summary>
    /// A module's layers. Reaching one of these is reaching behaviour rather than a name.
    /// </summary>
    private static readonly string[] LayerSegments = ["Application", "Persistence", "Infrastructure"];

    /// <summary>
    /// The namespaces that hold a module's <em>vocabulary</em> rather than its state — enumerations,
    /// value objects and strongly typed identifiers.
    /// <para>
    /// <b>This distinction is the one the audit collapsed, and it decides the remediation.</b> The
    /// finding counted all 107 crossings together and prescribed a read-model port for each. 59 of
    /// them reach only the namespaces below: <c>CommuteMode</c>, <c>DistanceUnit</c>,
    /// <c>GeoCoordinate</c>, <c>ProviderId</c>. A port around an enumeration buys nothing — the fix
    /// is to publish the type, because a name several modules must agree on is by definition not
    /// internal to one of them. Three edges reach actual aggregates and four reach another module's
    /// application layer; those are the ones a port is the right answer for.
    /// </para>
    /// </summary>
    private static readonly string[] VocabularySegments =
    [
        "Domain.Enums", "Domain.ValueObjects", "Domain.Identifiers", "Domain.Primitives", "Catalog"
    ];

    /// <summary>What closes an edge, derived from what the edge actually reaches.</summary>
    private enum Reach
    {
        /// <summary>Enumerations, value objects, identifiers. Closes by publishing the type.</summary>
        Vocabulary = 0,

        /// <summary>Another module's aggregates. Closes with a read port on its seam.</summary>
        Aggregate = 1,

        /// <summary>Another module's commands, queries or services. Closes with a published operation.</summary>
        Application = 2
    }

    /// <summary>One crossing that exists and is not yet allowed to.</summary>
    /// <param name="Consumer">The module doing the reaching.</param>
    /// <param name="Producer">The module being reached into.</param>
    /// <param name="Directives">
    /// How many <c>using</c> directives make up the edge. Exact, not a ceiling — see
    /// <see cref="A_module_reaches_another_module_only_through_its_published_seams"/>.
    /// </param>
    /// <param name="Reach">What the edge reaches, and therefore what would close it.</param>
    private sealed record InternalEdge(string Consumer, string Producer, int Directives, Reach Reach);

    /// <summary>
    /// The 110 crossings that exist, by edge, measured 18 November 2026.
    /// <para>
    /// <b>A dated exemption list, which is the technique this repository already uses for a
    /// boundary it has decided to hold without having finished moving behind it</b> —
    /// <c>AuthorizationConventionArchitectureTests.The_deferred_dotted_permission_keys_are_still_only_B13_and_B14</c>
    /// pins 25 keys so that a twenty-sixth is a decision somebody makes rather than a line somebody
    /// writes. The audit named that precedent as the shape to copy and this is it.
    /// </para>
    /// <para>
    /// <b>The counts are exact in both directions and that is the whole mechanism.</b> A crossing
    /// added to an edge fails the rule, which is the obvious half. A crossing <em>removed</em> and
    /// not decremented here also fails it, which is the half that does the work: it makes the list
    /// the current record of what is owed rather than a snapshot of November, and it turns the
    /// audit's second verification criterion — <em>the exemption list shrinks per wave and each
    /// removal is a named commit</em> — from a process promise into a build failure.
    /// </para>
    /// <para>
    /// <b>The audit counted 107 and there are 110, because three crossings are written as using
    /// aliases.</b> Both reports describe extracting <q>every <c>using Diaspora.Modules.X.…</c>
    /// directive</q>, which is a pattern anchored on the name following <c>using</c> — and
    /// <c>using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;</c> puts an alias in
    /// between. The three are that one in <c>JobGuideResolver</c> and
    /// <c>MarketplaceGuideResolver</c>, and
    /// <c>using LocationCountryCode = Diaspora.Modules.Location.Domain.ValueObjects.CountryCode;</c>
    /// in <c>GetHeatLayerQuery</c>. Two of them are whole edges missing from the finding
    /// (Jobs → Guides, Marketplace → Guides), which is why the audit reports 36 edges where there
    /// are 38.
    /// <br/>
    /// The form matters more than the arithmetic: an alias is what you reach for when the imported
    /// type's name <em>collides</em> with one of your own, so these are the most deliberate
    /// crossings in the codebase, and they were the ones the measurement could not see. Recorded
    /// here rather than corrected silently, because a scan and the number it produced belong
    /// together.
    /// </para>
    /// <para>
    /// Two rows of the audit's sample table are also low against a full count — it lists 15 for
    /// GeographicIntelligence → Location where there are 18, and 7 for Housing → Location where
    /// there are 8 — so that table is a sample of the heaviest edges rather than a census.
    /// </para>
    /// </summary>
    private static readonly InternalEdge[] EdgesStillOwed =
    [
        // Application — another module's commands, queries or services (4 edges, 31 directives).
        new("GeographicIntelligence", "Location", 18, Reach.Application),
        new("Housing", "Location", 8, Reach.Application),
        new("CommunityProgramme", "Financial", 4, Reach.Application),
        new("Creator", "Media", 1, Reach.Application),

        // Aggregate — another module's entities (3 edges, 18 directives).
        new("Guides", "Community", 10, Reach.Aggregate),
        new("CommunityLife", "Location", 6, Reach.Aggregate),
        new("Creator", "Community", 2, Reach.Aggregate),

        // Vocabulary — enumerations, value objects, identifiers (31 edges, 61 directives).
        new("Community", "Media", 4, Reach.Vocabulary),
        new("CommunityLife", "Community", 4, Reach.Vocabulary),
        new("Providers", "Community", 4, Reach.Vocabulary),
        new("Housing", "Media", 3, Reach.Vocabulary),
        new("Jobs", "Location", 3, Reach.Vocabulary),
        new("Marketplace", "Location", 3, Reach.Vocabulary),
        new("Marketplace", "Media", 3, Reach.Vocabulary),
        new("Providers", "Media", 3, Reach.Vocabulary),
        new("Community", "Location", 2, Reach.Vocabulary),
        new("CommunityLife", "Providers", 2, Reach.Vocabulary),
        new("CommunityProgramme", "Community", 2, Reach.Vocabulary),
        new("Conversations", "Community", 2, Reach.Vocabulary),
        new("Guides", "Location", 2, Reach.Vocabulary),
        new("Housing", "Community", 2, Reach.Vocabulary),
        new("Housing", "Providers", 2, Reach.Vocabulary),
        new("Integrations", "Notifications", 2, Reach.Vocabulary),
        new("Jobs", "Community", 2, Reach.Vocabulary),
        new("Marketplace", "Community", 2, Reach.Vocabulary),
        new("Providers", "Location", 2, Reach.Vocabulary),
        new("Automation", "Notifications", 1, Reach.Vocabulary),
        new("CommunityLife", "Guides", 1, Reach.Vocabulary),
        new("CommunityLife", "Media", 1, Reach.Vocabulary),
        new("Conversations", "Notifications", 1, Reach.Vocabulary),
        new("Guides", "Media", 1, Reach.Vocabulary),
        new("Guides", "Providers", 1, Reach.Vocabulary),
        new("Housing", "Guides", 1, Reach.Vocabulary),
        new("Jobs", "Guides", 1, Reach.Vocabulary),
        new("Jobs", "Media", 1, Reach.Vocabulary),
        new("Jobs", "Providers", 1, Reach.Vocabulary),
        new("Marketplace", "Guides", 1, Reach.Vocabulary),
        new("Marketplace", "Providers", 1, Reach.Vocabulary)
    ];

    /// <summary>
    /// What both audit reports counted, independently of the table above. Pinned so that a wave
    /// which closes edges records how far the list has come from where it started.
    /// </summary>
    private const int CrossingsTheAuditCounted = 107;

    /// <summary>
    /// The three the audit's directive pattern could not see, because they are written as using
    /// aliases. Declared as its own number rather than folded into the total, so that the next
    /// person to re-measure and get 110 can see which 3 are the difference.
    /// </summary>
    private const int AliasedCrossingsTheAuditMissed = 3;

    /// <summary>
    /// The ceiling the list may never exceed. A list that sums above this means crossings were
    /// added after the finding was raised, which is the thing the ratchet exists to prevent.
    /// </summary>
    private const int CrossingsFound = CrossingsTheAuditCounted + AliasedCrossingsTheAuditMissed;

    private static readonly Lazy<Crossings> Scanned = new(ScanTheModules);

    /// <summary>One reach into another module, and where it is written.</summary>
    private sealed record Crossing(string Consumer, string Producer, string Reached, string Where);

    /// <summary>
    /// What the scan found. <paramref name="Internal"/> and <paramref name="WrittenInFull"/> are
    /// the same violation written two different ways, kept apart because only the first is
    /// exempted — see
    /// <see cref="No_crossing_is_hidden_behind_a_fully_qualified_name"/>.
    /// </summary>
    private sealed record Crossings(
        List<Crossing> Internal,
        List<Crossing> WrittenInFull,
        List<Crossing> Published,
        HashSet<string> Modules,
        int Files);

    // ── The rules ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The rule.</b> A module reaches another module through <c>Contracts</c>,
    /// <c>Extensibility</c> or <c>Integration</c>, and the crossings that do not are the ones on
    /// the dated list — at exactly the count recorded there.
    /// <para>
    /// Three ways to fail, three different sentences, because the fix is different each time: a new
    /// edge appeared, an existing edge grew, or an edge shrank and the list was not updated with it.
    /// </para>
    /// </summary>
    [Fact]
    public void A_module_reaches_another_module_only_through_its_published_seams()
    {
        var declared = EdgesStillOwed.ToDictionary(
            edge => (edge.Consumer, edge.Producer),
            edge => edge.Directives);

        var found = Scanned.Value.Internal
            .GroupBy(crossing => (crossing.Consumer, crossing.Producer))
            .ToDictionary(group => group.Key, group => group.ToList());

        var offenders = new List<string>();

        foreach (var (edge, crossings) in found.OrderBy(entry => entry.Key))
        {
            if (!declared.TryGetValue(edge, out var allowed))
            {
                offenders.Add(
                    $"NEW EDGE {edge.Consumer} -> {edge.Producer} ({crossings.Count}): reach " +
                    $"{edge.Producer} through its Contracts, Extensibility or Integration " +
                    $"namespace. If it genuinely cannot be reached that way, the seam is missing " +
                    $"and adding the edge to EdgesStillOwed is a decision, not a formality. " +
                    string.Join(", ", crossings.Select(crossing => crossing.Where)));

                continue;
            }

            if (crossings.Count > allowed)
            {
                offenders.Add(
                    $"GREW {edge.Consumer} -> {edge.Producer}: {allowed} recorded, " +
                    $"{crossings.Count} found. Added: " +
                    string.Join(", ", crossings.Select(crossing => crossing.Where)));
            }
            else if (crossings.Count < allowed)
            {
                offenders.Add(
                    $"SHRANK {edge.Consumer} -> {edge.Producer}: {allowed} recorded, " +
                    $"{crossings.Count} found. Decrement it in the commit that closed them, so " +
                    "the list stays the record of what is still owed.");
            }
        }

        foreach (var edge in declared.Keys.Except(found.Keys).OrderBy(edge => edge))
        {
            offenders.Add(
                $"CLOSED {edge.Consumer} -> {edge.Producer}: {declared[edge]} recorded, none " +
                "found. Remove the entry in the commit that closed the edge.");
        }

        offenders.Should().BeEmpty(
            "a module's Domain, Application and Persistence are its own; the list is a ratchet " +
            "and it has to be true in both directions to be one:\n" +
            string.Join("\n", offenders));
    }

    /// <summary>
    /// <b>The exemption is for an edge, not for a spelling.</b> Every crossing on the list is a
    /// <c>using</c> directive, so a rule that only read <c>using</c> directives could be satisfied
    /// by writing the type's full name instead — the same dependency, invisible to the ratchet, and
    /// the obvious thing to reach for when the build goes red.
    /// <para>
    /// There are none today, which is the point of asserting it now: this costs nothing while it is
    /// true and would cost a redesign later.
    /// </para>
    /// </summary>
    [Fact]
    public void No_crossing_is_hidden_behind_a_fully_qualified_name()
    {
        var offenders = Scanned.Value.WrittenInFull
            .Select(crossing =>
                $"{crossing.Where}: {crossing.Consumer} names " +
                $"{ModulePrefix}{crossing.Producer}.{crossing.Reached} in full")
            .Order(StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "a fully qualified name is the same crossing with the using directive left out:\n" +
            string.Join("\n", offenders));
    }

    /// <summary>
    /// <b>The scanner is checked against every spelling, because a scanner that under-matches looks
    /// exactly like a clean codebase.</b> The rule above reports zero today. If the scan simply
    /// could not see a fully qualified name it would report zero forever and read as a pass — and
    /// the ratchet could not catch that, because a crossing the scan does not see produces no
    /// requirement at all.
    /// <para>
    /// <b>This is not hypothetical in either direction.</b> The first version of the fully
    /// qualified rule tested <c>Ancestors()</c> for a namespace declaration, which under a
    /// file-scoped namespace is every node in the file; it reported zero because it examined
    /// nothing, and the probe below is the only reason that was noticed. Separately, the audit's
    /// own measurement and the script that reproduced it both anchored on the name following
    /// <c>using</c> and so missed all three alias crossings — the same failure, one level up.
    /// </para>
    /// <para>
    /// The probes run the same <see cref="ReadOneFile"/> the production scan runs. A probe against
    /// a second copy of the logic would prove something about the copy.
    /// </para>
    /// </summary>
    [Fact]
    public void The_scanner_sees_a_crossing_however_it_is_written()
    {
        const string viaUsingDirective = """
            using Diaspora.Modules.Location.Domain.Areas;
            namespace Diaspora.Modules.Housing.Probe;
            internal sealed class Probe { public Area? Value { get; set; } }
            """;

        const string viaAlias = """
            using LocationArea = Diaspora.Modules.Location.Domain.Areas.Area;
            namespace Diaspora.Modules.Housing.Probe;
            internal sealed class Probe { public LocationArea? Value { get; set; } }
            """;

        const string viaStaticImport = """
            using static Diaspora.Modules.Location.Domain.Spatial.Geodesy;
            namespace Diaspora.Modules.Housing.Probe;
            internal sealed class Probe { public double Value => Distance(1, 2); }
            """;

        const string viaFullName = """
            namespace Diaspora.Modules.Housing.Probe;
            internal sealed class Probe
            {
                public Diaspora.Modules.Location.Domain.Areas.Area? Value { get; set; }
            }
            """;

        const string viaThePublishedSeam = """
            using Diaspora.Modules.Location.Extensibility;
            namespace Diaspora.Modules.Housing.Probe;
            internal sealed class Probe
            {
                public INearbyResourceProvider? Value { get; set; }
                public Diaspora.Modules.Location.Contracts.V1.Responses.Anything? Other { get; set; }
            }
            """;

        var byDirective = Probe(viaUsingDirective);
        byDirective.Internal.Should().ContainSingle(
            "the directive form is what the exemption list is written in");
        byDirective.WrittenInFull.Should().BeEmpty("the type is reached through the directive");

        var byAlias = Probe(viaAlias);
        byAlias.Internal.Should().ContainSingle(
            "an alias is a using directive, and three of the platform's crossings are written " +
            "this way — the form the audit's own measurement could not see");
        byAlias.WrittenInFull.Should().BeEmpty("the alias target is the directive's own name");

        var byStaticImport = Probe(viaStaticImport);
        byStaticImport.Internal.Should().ContainSingle(
            "a static import reaches a type the same as any other directive");

        var byFullName = Probe(viaFullName);
        byFullName.WrittenInFull.Should().ContainSingle(
            "a fully qualified name is a crossing the exemption list cannot describe");
        byFullName.Internal.Should().BeEmpty("there is no directive to attribute it to");

        var bySeam = Probe(viaThePublishedSeam);
        bySeam.Internal.Should().BeEmpty("Extensibility is published");
        bySeam.WrittenInFull.Should().BeEmpty("Contracts is published, written in full or not");
        bySeam.Published.Should().HaveCount(2, "both spellings name a published namespace");
    }

    /// <summary>
    /// <b>The list is held to the number the audit reached, which came from outside this
    /// repository.</b> A total recomputed from the list itself would accept any list; this one says
    /// the 107 the two reports counted independently are all accounted for, and a wave that closes
    /// edges has to say so here.
    /// </summary>
    [Fact]
    public void The_exemption_list_accounts_for_every_crossing_the_audit_counted()
    {
        EdgesStillOwed.Select(edge => (edge.Consumer, edge.Producer))
            .Should().OnlyHaveUniqueItems("one entry per edge, or two entries disagree silently");

        EdgesStillOwed.Should().OnlyContain(
            edge => edge.Directives > 0, "an edge with no crossings is a closed edge");

        var known = Scanned.Value.Modules;

        EdgesStillOwed.SelectMany(edge => new[] { edge.Consumer, edge.Producer })
            .Distinct(StringComparer.Ordinal)
            .Should().OnlyContain(
                module => known.Contains(module),
                "an entry naming a module that does not exist exempts nothing. Modules: " +
                string.Join(", ", known.Order(StringComparer.Ordinal)));

        EdgesStillOwed.Sum(edge => edge.Directives)
            .Should().BeLessThanOrEqualTo(
                CrossingsFound,
                "the list may shrink below the {0} measured here and must never exceed it: a " +
                "higher number means crossings were added after the finding was raised. The " +
                "audit's own figure was {1}, short by the {2} written as using aliases",
                CrossingsFound,
                CrossingsTheAuditCounted,
                AliasedCrossingsTheAuditMissed);
    }

    /// <summary>
    /// <b>Each entry says what would close it, and the claim is checked rather than trusted.</b>
    /// The classification is derived from the namespaces the edge actually reaches, so an edge
    /// recorded as reaching only vocabulary that starts reaching an aggregate fails here — which is
    /// the case that matters, because the two have different fixes and the cheap one is the common
    /// one.
    /// <para>
    /// It also retires the field: an edge whose remaining crossings are all vocabulary gets
    /// reclassified by the same commit that removed the rest, which keeps the list honest about
    /// what is left rather than about what it was in November.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_exemption_declares_what_would_close_it()
    {
        var reached = Scanned.Value.Internal
            .GroupBy(crossing => (crossing.Consumer, crossing.Producer))
            .ToDictionary(
                group => group.Key,
                group => group.Max(crossing => Classify(crossing.Reached)));

        var wrong = EdgesStillOwed
            .Where(edge => reached.TryGetValue((edge.Consumer, edge.Producer), out var actual)
                && actual != edge.Reach)
            .Select(edge =>
                $"{edge.Consumer} -> {edge.Producer} is recorded as {edge.Reach} and reaches " +
                $"{reached[(edge.Consumer, edge.Producer)]}")
            .ToList();

        wrong.Should().BeEmpty(
            "the Reach field is the remediation, not a label: Vocabulary closes by publishing the " +
            "type, Aggregate by a read port, Application by a published operation:\n" +
            string.Join("\n", wrong));
    }

    /// <summary>
    /// <b>What the scan read, so that none of the rules above can pass by having found nothing.</b>
    /// Every one of them is an assertion that a collection is empty or that a dictionary agrees
    /// with a list, and all of those are satisfied by a scan that read no files at all — a renamed
    /// directory, a changed path prefix, a filter that excluded everything.
    /// </summary>
    [Fact]
    public void The_scan_read_the_platforms_modules()
    {
        Scanned.Value.Modules.Should().HaveCountGreaterThanOrEqualTo(
            21, "twenty-one module projects have shipped between B3 and B23");

        Scanned.Value.Files.Should().BeGreaterThan(
            2_000, "the module projects are the bulk of the platform's source");

        Scanned.Value.Published.Should().HaveCountGreaterThan(
            100,
            "the published seams are used more often than they are bypassed, which is what makes " +
            "this a boundary that exists rather than one that was never built");

        Scanned.Value.Internal.Should().HaveCountGreaterThan(
            0, "a scan that finds no crossings at all has stopped reading the source");
    }

    // ── The scan ─────────────────────────────────────────────────────────

    private static Crossings ScanTheModules()
    {
        var crossings = new Crossings([], [], [], new HashSet<string>(StringComparer.Ordinal), 0);
        var files = 0;

        foreach (var file in SourceTree.ProductionFiles)
        {
            if (!file.Path.StartsWith(ModuleSourcePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var consumer = file.Path[ModuleSourcePrefix.Length..].Split('/')[0];
            crossings.Modules.Add(consumer);
            files++;

            ReadOneFile(file, consumer, crossings);
        }

        return crossings with { Files = files };
    }

    /// <summary>
    /// Reads one file's crossings into <paramref name="into"/>.
    /// <para>
    /// Shared by the production scan and by <see cref="The_scanner_sees_a_crossing_written_either_way"/>,
    /// which is the point: a probe that exercised a second copy of this logic would prove something
    /// about the copy.
    /// </para>
    /// </summary>
    private static void ReadOneFile(SourceFile file, string consumer, Crossings into)
    {
        var imported = new HashSet<string>(StringComparer.Ordinal);

        foreach (var directive in file.Root.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            var named = directive.Name?.ToString();

            if (!TryReadCrossing(named, consumer, out var producer, out var reached))
            {
                continue;
            }

            imported.Add($"{ModulePrefix}{producer}.{reached}");

            var crossing = new Crossing(consumer, producer, reached, file.Locate(directive));

            (IsPublished(reached) ? into.Published : into.Internal).Add(crossing);
        }

        foreach (var node in file.Root.DescendantNodes())
        {
            if (node is not (QualifiedNameSyntax or MemberAccessExpressionSyntax))
            {
                continue;
            }

            // Only the outermost link of a dotted chain, or one name is counted five times.
            if (node.Parent is QualifiedNameSyntax or MemberAccessExpressionSyntax)
            {
                continue;
            }

            // A using directive is handled above and a namespace declaration is the file's own
            // name, so the outermost dotted name directly under either is not a crossing in code.
            //
            // The parent, emphatically not an ancestor: a file-scoped namespace declaration is an
            // ancestor of every node in the file, so an Ancestors() test here matches everything
            // and the rule silently reports nothing. It did, and the probe below is what said so.
            if (node.Parent is UsingDirectiveSyntax
                or NamespaceDeclarationSyntax
                or FileScopedNamespaceDeclarationSyntax)
            {
                continue;
            }

            var written = Unspaced(node.ToString());

            if (!TryReadCrossing(written, consumer, out var producer, out var reached))
            {
                continue;
            }

            var crossing = new Crossing(consumer, producer, reached, file.Locate(node));

            if (IsPublished(reached))
            {
                into.Published.Add(crossing);
                continue;
            }

            // A full name whose namespace this file already imports is the directive's crossing
            // written out again, not a second one, and the directive is already recorded.
            var alreadyImported = imported.Any(namespaceName =>
                string.Equals(written, namespaceName, StringComparison.Ordinal) ||
                written.StartsWith(namespaceName + ".", StringComparison.Ordinal));

            if (!alreadyImported)
            {
                into.WrittenInFull.Add(crossing);
            }
        }
    }

    /// <summary>
    /// Splits a name of the form <c>Diaspora.Modules.{Producer}.{Reached}</c>, rejecting anything
    /// that is not another module's.
    /// </summary>
    private static bool TryReadCrossing(
        string? named, string consumer, out string producer, out string reached)
    {
        producer = string.Empty;
        reached = string.Empty;

        if (named is null || !named.StartsWith(ModulePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var segments = named[ModulePrefix.Length..].Split('.');

        // "Diaspora.Modules.Location" on its own names no namespace inside the module.
        if (segments.Length < 2)
        {
            return false;
        }

        producer = segments[0];

        if (string.Equals(producer, consumer, StringComparison.Ordinal))
        {
            return false;
        }

        reached = string.Join('.', segments.Skip(1));

        return true;
    }

    private static bool IsPublished(string reached) =>
        PublishedSegments.Contains(FirstSegmentOf(reached), StringComparer.Ordinal);

    private static Reach Classify(string reached)
    {
        if (LayerSegments.Contains(FirstSegmentOf(reached), StringComparer.Ordinal))
        {
            return Reach.Application;
        }

        var vocabulary = VocabularySegments.Any(segment =>
            string.Equals(reached, segment, StringComparison.Ordinal) ||
            reached.StartsWith(segment + ".", StringComparison.Ordinal));

        return vocabulary ? Reach.Vocabulary : Reach.Aggregate;
    }

    private static string FirstSegmentOf(string reached)
    {
        var dot = reached.IndexOf('.', StringComparison.Ordinal);

        return dot < 0 ? reached : reached[..dot];
    }

    /// <summary>A dotted name may be written across lines; the whitespace is not part of it.</summary>
    private static string Unspaced(string written) =>
        written.Any(char.IsWhiteSpace)
            ? string.Concat(written.Where(character => !char.IsWhiteSpace(character)))
            : written;

    private static Crossings Probe(string source)
    {
        var file = new SourceFile(
            "probe/Probe.cs", CSharpSyntaxTree.ParseText(source).GetRoot());

        var crossings = new Crossings([], [], [], new HashSet<string>(StringComparer.Ordinal), 0);

        // Every probe is written as if it were a Housing file reaching Location.
        ReadOneFile(file, "Housing", crossings);

        return crossings;
    }
}
