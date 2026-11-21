using Diaspora.Contracts.Financial.V1.Advertising;
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
/// ratchet rather than a clean assertion: there are 138 crossings today and a rule that simply
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

    /// <summary>
    /// The same prefix as C# sees it from inside a module: <c>Diaspora</c> is an enclosing
    /// namespace, so <c>Modules</c> alone binds to <c>Diaspora.Modules</c>.
    /// </summary>
    private const string RelativeModulePrefix = "Modules.";

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
    /// finding counted all 107 crossings together and prescribed a read-model port for each. 58 of
    /// the 138 reach only the namespaces below: <c>CommuteMode</c>, <c>DistanceUnit</c>,
    /// <c>GeoCoordinate</c>, <c>ProviderId</c>. A port around an enumeration buys nothing — the fix
    /// is to publish the type, because a name several modules must agree on is by definition not
    /// internal to one of them. Three edges reach actual aggregates and eleven reach another
    /// module's application layer; those are the ones a port is the right answer for.
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
    /// <param name="InCode">
    /// How many crossings are written inline instead, with no directive to show for them.
    /// <para>
    /// <b>Its own column because this is the half nobody could see.</b> A reader checking whether a
    /// module reaches another one looks at the top of the file, and 28 of these crossings are not
    /// there — they are <c>Modules.Media.Application.Assets.AttachMediaCommand</c> in the middle of
    /// a method. Counting them in with the directives would have recorded the total and hidden the
    /// fact that the usual way of finding them does not work.
    /// </para>
    /// </param>
    /// <param name="Reach">What the edge reaches, and therefore what would close it.</param>
    private sealed record InternalEdge(
        string Consumer, string Producer, int Directives, int InCode, Reach Reach)
    {
        public int Crossings => Directives + InCode;
    }

    /// <summary>
    /// The 138 crossings that exist, by edge, measured 18 November 2026.
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
    /// <b>The audit counted 107 and there are 138. Both measurements that produced 107 were
    /// written the same way, and that is the whole explanation.</b> The reports describe extracting
    /// <q>every <c>using Diaspora.Modules.X.…</c> directive</q> — a pattern anchored on the name
    /// following <c>using</c>, in files whose imports are fully qualified. It is exactly right
    /// about the crossings that look like that and blind to every other kind:
    /// <list type="bullet">
    ///   <item><b>+3 written as aliases.</b>
    ///   <c>using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;</c> puts a name
    ///   between <c>using</c> and the namespace. Two of the three are whole edges missing from the
    ///   finding — Jobs → Guides and Marketplace → Guides — which is why it reports 36 edges where
    ///   there are 38.</item>
    ///   <item><b>+28 that are not directives at all.</b>
    ///   <c>Modules.Media.Application.Assets.AttachMediaCommand</c> in the middle of a method, or
    ///   <c>Location.Domain.ValueObjects.GeoCoordinate</c>, both of which compile from inside
    ///   <c>Diaspora.Modules</c>. <c>FeedComposer</c> reaches Location six times this way and has
    ///   no directive naming it.</item>
    /// </list>
    /// <br/>
    /// <b>Neither blind spot is random, and that is what made them expensive.</b> An alias is what
    /// you write when the imported name <em>collides</em> with one of your own; an inline reference
    /// is what you write when you need one type and do not want the import. Both are the
    /// <em>deliberate</em> crossings. And the 28 change the finding rather than its arithmetic:
    /// seven module → Media edges are <see cref="Reach.Application"/> entirely because of them,
    /// because every module with media attachments constructs Media's application command directly
    /// — which is a thing worth knowing and which no count of imports could show.
    /// </para>
    /// <para>
    /// Two rows of the audit's sample table are also low against a full count — it lists 15 for
    /// GeographicIntelligence → Location where there are 18, and 7 for Housing → Location where
    /// there are 8 — so that table is a sample of the heaviest edges rather than a census.
    /// </para>
    /// <para>
    /// <b>The first wave closed nine edges by changing two seams, not nine call sites.</b> Every
    /// crossing into Guides and into Providers was <em>forced</em> by a published seam:
    /// <c>IGuideDirectory</c> was typed on B8's <c>GuideId</c>, and <c>IProviderDirectory</c> on
    /// B7's <c>ProviderId</c> and <c>ProviderRole</c>, so a consumer could not call the seam it
    /// had been given without importing the internals behind it. Each of the nine consumers is a
    /// resolver that already held a <see cref="Guid"/> and converted it up to satisfy the
    /// signature — and both seams already returned dictionaries keyed by <see cref="Guid"/>, so
    /// each spoke the published type in one direction and the internal one in the other.
    /// Identifiers now cross as <see cref="Guid"/>, converted on the owning module's side, and
    /// the role is published as <c>Diaspora.Contracts.Providers.V1.ProviderRole</c>. <b>No module
    /// reaches into Guides or into Providers at all.</b> 138 crossings over 38 edges became 127
    /// over 29.
    /// </para>
    /// <para>
    /// <b>The second wave closed seven more by publishing a vocabulary rather than changing a
    /// signature.</b> B6's <c>ContentReference</c> — a <c>(CommunityContentType, Guid)</c> pair —
    /// is named by seven of B6's ten published files, and its own summary calls it <q>the type
    /// that makes B6 reusable</q>: every module that contributes content to the social surface
    /// implements <c>ICommunityContentSource</c> and must construct one. <b>No primitive can
    /// stand in for it</b>, so the wave-1 move does not apply; the answer is the one the audit's
    /// own vocabulary class prescribes, which is to publish the type. <c>ContentReference</c>,
    /// <c>CommunityContentType</c>, <c>VisibilityLevel</c>, <c>FollowTargetType</c> and
    /// <c>ReportReasonCode</c> are now <c>Diaspora.Contracts.Community.V1</c>, and B6's published
    /// surface went from thirteen internal references to four.
    /// </para>
    /// <para>
    /// <c>PollStatus</c> was deliberately <em>not</em> published. B6 and B10 both declare one, and
    /// the single file that meets both aliases its own — so B6's is reached by nobody, and
    /// publishing it would have created a fresh ambiguity in the one file that already works
    /// around the duplicate. <b>A name only one module uses is not shared vocabulary</b>, however
    /// much it looks like its neighbours.
    /// </para>
    /// <para>
    /// <b>The third wave published B4's vocabulary and closed five more.</b> Eleven types —
    /// <c>CountryCode</c>, <c>GeoCoordinate</c>, <c>BoundingBox</c>, <c>Distance</c>,
    /// <c>HierarchyPath</c>, <c>LocationSlug</c> and the five enumerations they and their callers
    /// need — are now <c>Diaspora.Contracts.Location.V1</c>. The dependency closure decided the
    /// list rather than the crossing counts did: <c>Distance</c> needs <c>DistanceUnit</c> and
    /// <c>GeoCoordinate</c> needs <c>CoordinatePrecision</c>, and <b>a leaf assembly cannot
    /// reach back into the module it was extracted from</b>, so those two travel whether or not
    /// anyone else names them. That is a different reason from the one that keeps
    /// <c>PollStatus</c> in B6 — necessity rather than demand.
    /// </para>
    /// <para>
    /// <c>LocationErrorCodes</c> moved for the same reason. <c>CountryCode</c> and its neighbours
    /// return <c>Result</c> failures carrying those codes, so <b>a published type has to be able
    /// to state its failures in published terms</b>. The file already described itself as frozen
    /// alongside the API and changed only with a new API version, which is what <c>V1</c> means;
    /// the strings are untouched. The other twenty modules keep theirs at their own root, and the
    /// asymmetry is real: B4 is simply the first module whose vocabulary was published.
    /// </para>
    /// <para>
    /// <b>Both of B4's remaining consumers reach past the vocabulary, which is why they survive.</b>
    /// <c>ILocationDirectory</c> returns <c>Country</c> and <c>AdministrativeArea</c> — whole
    /// aggregates — so GeographicIntelligence and Housing still reach <c>Domain.Countries</c> and
    /// <c>Domain.Areas</c>, and CommunityLife reaches both. No amount of vocabulary publishing
    /// touches that; it needs the seam to answer with a projection, which is a change to what
    /// consumers receive rather than to where a type lives.
    /// </para>
    /// <para>
    /// <b>The fourth wave closed every edge into Media, and with them the last inline crossing on
    /// the platform.</b> B5 had published the payload and never published the operation: seven
    /// verticals each built a frozen <c>AttachMediaRequest</c> — B5's own V1 contract — then
    /// wrapped it in <c>AttachMediaCommand</c>, B5's <em>application</em> layer, and dispatched
    /// that. Each of those seven classes carries a doc comment saying the module consumes B5's
    /// frozen contract and edits nothing in B5, and each was right about the intent; the envelope
    /// was the leak, and it is the reason seven edges were classified
    /// <see cref="Reach.Application"/> rather than <see cref="Reach.Vocabulary"/>.
    /// <c>Media.Integration.IMediaAttachments</c> now carries the request, and
    /// <c>MediaOwnerType</c>, <c>MediaAccessLevel</c> and <c>MediaReferenceRoles</c> are published
    /// beside it. <b>No module reaches into Media, Guides or Providers at all.</b>
    /// </para>
    /// <para>
    /// <see cref="InlineCrossingsNobodyCounted"/> is therefore now zero in the source, and the
    /// constant stays because it records what the audit's measurement could not see rather than
    /// what is there today. <see cref="The_scan_read_the_platforms_modules"/> asserts the
    /// emptiness instead of a count above zero, which makes it a ratchet against the next one.
    /// </para>
    /// <para>
    /// <b>The eighth wave closed the last three, and the list is empty.</b> Each needed a
    /// different answer, which is a fair summary of the whole finding. <c>Creator</c> injected
    /// B6's <c>IFollowRepository</c> — the type that can also follow, unfollow and mute — to ask
    /// two read questions, so <c>ICommunityFollowers</c> publishes the three that are reads.
    /// <c>GuideDiscussionService</c> named B6's <c>CommentThread</c> in a <c>GetThreadAsync</c>
    /// <b>that had no caller</b>: the thread is served to clients by B6's own endpoint and B8 only
    /// ever needed the count, so the shortest honest way to stop naming another module's aggregate
    /// was to stop asking for one. And <c>GetHeatLayerQuery</c> named <c>MapCellAggregator</c>
    /// seven times for four static grid methods, which is <see cref="MapGrid"/> now — pure
    /// geometry, in <c>Extensibility</c>, shared with the cluster endpoint so that a cell key
    /// means one thing on this platform.
    /// </para>
    /// <para>
    /// <b>That extraction was attempted and reverted a day earlier</b>, because the geometry was
    /// entangled with the service's private constants and with the bounds parsing in
    /// <c>Domain.Search</c>. It is here now because the constants came with it and
    /// <c>BoundingBoxSearch</c> was published beside the box it parses — which is the general
    /// shape of this finding's last two waves: <b>the thing in the way was never the consumer</b>.
    /// </para>
    /// <para>
    /// <b>The seventh wave closed B4's read surface and fourteen of its fifteen crossings.</b>
    /// The keystone was the largest yet and the cheapest to fix: every aggregate-returning
    /// method on <c>ILocationDirectory</c> <b>had no caller outside B4 at all</b> — all eleven
    /// external consumers called <c>DistanceBetween</c> and nothing else — while three modules
    /// had given up on the seam and injected <c>IAdministrativeAreaRepository</c> directly,
    /// which is the one thing that interface's own summary says it exists to prevent.
    /// <c>AreaDescriptor</c> and <c>CountryDescriptor</c> are the projections, and the four
    /// country consumers between them read <b>one field</b>.
    /// </para>
    /// <para>
    /// <b>A crossing scan sees none of that, which is why debt is counted where it is owed.</b>
    /// A consumer writing <c>var area = await _directory.FindAreaAsync(...)</c> uses B4's
    /// aggregate without ever naming it, so no directive and no qualification is recorded
    /// anywhere. <see cref="SurfacesStillLeaking"/> counted it in the one place it could be
    /// fixed, and the eleven consumers cost nothing to carry across.
    /// </para>
    /// <para>
    /// <b>The sixth wave closed B15, and it took three remedies rather than one.</b> Four
    /// crossings, three shapes. <c>CommunityTargetingContributors</c> was the keystone again:
    /// <c>IAdTargetingOptionProvider.Dimension</c> is an <c>AdTargetingDimension</c> and
    /// <c>IAdAudienceSizer.EstimateAsync</c> takes an <c>AdAudience</c>, both internal, so the
    /// contributor seam obliged its own implementor while its summary argued that B15 <i>defines
    /// the shape</i> and <i>whoever owns the data implements it</i>. Both are published now.
    /// <c>MembershipSettlement</c> and <c>SaveMembershipProgramme</c> injected two concrete
    /// service classes, which became <c>IMembershipPayments</c> and <c>ICurrencies</c>. And
    /// <c>CampaignEndpoints</c> constructed thirteen of B15's command envelopes, which is wave
    /// four's shape exactly: <b>B15 had published every request and response they carry and had
    /// nowhere to put the operation</b>, having — like B5 and B13 before it — no
    /// <c>Integration</c> namespace at all.
    /// </para>
    /// <para>
    /// <b>That is three producers in a row whose keystone was the absence of a namespace.</b> A
    /// module that publishes payloads and contributor seams but never an operation leaves its
    /// consumers no way to call it except through its application layer — and the count then
    /// records three careless consumers where there was one producer with nowhere to be called.
    /// </para>
    /// <para>
    /// <b>The fifth wave closed the vocabulary class entirely.</b> B13's published
    /// <c>Extensibility</c> seams named seven of its own <c>Domain.Enums</c> types across sixteen
    /// files, so every notification-category contributor was obliged to import them:
    /// <c>NotificationCategorySeed.DefaultPriority</c> is a <c>NotificationPriority</c>, and a
    /// module declaring a category has no way to set it without reaching past the seam it was
    /// handed. The seven are published to <c>Diaspora.Contracts.Notifications.V1</c>; the five
    /// left behind — <c>NotificationStatus</c>, <c>DeliveryStatus</c>, <c>DigestStatus</c>,
    /// <c>TemplateStatus</c> and <c>ArchiveReason</c> — are lifecycle state the module owns, and
    /// nothing outside B13 names any of them. <b>The division already existed; only the namespace
    /// disagreed with it.</b>
    /// </para>
    /// <para>
    /// <see cref="A_published_read_surface_does_not_oblige_a_consumer_to_reach_past_it"/> could
    /// not have found this one. It scans <c>Integration</c>, and <b>B13 has no
    /// <c>Integration</c> namespace at all</b> — its seams are contributor-facing, so they live in
    /// <c>Extensibility</c>, which that rule excludes for a stated reason: a contributor is handed
    /// the module's own aggregate on purpose. The reason holds for an aggregate and does not hold
    /// for a <em>seed</em>, which the contributor constructs rather than receives, so every
    /// internal type in one is an obligation.
    /// <see cref="A_contributor_seed_does_not_name_the_modules_internals"/> is that narrower line,
    /// and it is narrower than the exclusion it refines rather than a reversal of it.
    /// </para>
    /// <para>
    /// <b>That is the shape of the rest of this finding rather than a special case.</b> Both
    /// reports prescribed a read-model port for each of the 107 crossings — and these nine
    /// consumers were already using the port. Where a published seam names an internal type, no
    /// amount of work at the call site can close the edge, and one change to the seam closes
    /// every consumer of it at once. Two such types are left in B7's surface:
    /// <c>IProviderCatalog</c> names <c>ServiceCategoryId</c> and
    /// <c>IProviderDirectory.IsVerifiedAsync</c> names <c>ProviderVerificationKind</c>. Both are
    /// recorded in those files; no module reaches either today, and
    /// <see cref="A_published_read_surface_does_not_oblige_a_consumer_to_reach_past_it"/> holds
    /// the line at the count that is there.
    /// </para>
    /// </summary>
    /// <summary>
    /// <b>Empty, since 21 November 2026.</b> Every entry that was here has been closed, and the
    /// list stays as a list because a <see cref="InternalEdge"/> appearing in it again is how the
    /// next one would be recorded — deliberately, with a date and a remediation, rather than by
    /// quietly widening a rule.
    /// </summary>
    private static readonly InternalEdge[] EdgesStillOwed = [];

    /// <summary>One module's published read surface, and the internal references left in it.</summary>
    /// <param name="Module">The module that publishes the surface.</param>
    /// <param name="References">
    /// How many times its <c>Integration</c> namespace reaches its own internals. Exact rather
    /// than a ceiling, for the same reason the edge counts are.
    /// </param>
    private sealed record SeamDebt(string Module, int References);

    /// <summary>
    /// The published read surfaces that still name a type behind them, by module.
    /// <para>
    /// Measured by the rule that enforces it rather than by a second scan written to agree with
    /// it. Guides is absent because the first wave emptied it, and Location because the seventh
    /// did -- <c>ILocationDirectory</c> answered with <c>Country</c> and <c>AdministrativeArea</c>
    /// until 21 November 2026, which is the seam written to stop a vertical reading B4's
    /// internals handing them over. Providers is still here at two,
    /// which is what the summary above records and what this sentence said otherwise until
    /// 21 November 2026.
    /// </para>
    /// </summary>
    private static readonly SeamDebt[] SurfacesStillLeaking =
    [
        new("Community", 4),
        new("Conversations", 4),
        new("Providers", 2)
    ];

    /// <summary>
    /// The contributor seeds that still name their own module's internals, by module.
    /// <para>
    /// Measured by the rule below. B13's six were emptied on 21 November 2026 by publishing the
    /// vocabulary they named; these eight are the same shape and have the same remedy.
    /// </para>
    /// </summary>
    private static readonly SeamDebt[] SeedsStillLeaking =
    [
        new("Financial", 4),
        new("Guides", 1),
        new("Marketplace", 1),
        new("Recommendations", 2)
    ];

    /// <summary>A module's own layers. Reaching one of these from its own seam is the debt.</summary>
    private static readonly string[] InnerSegments =
        ["Domain", "Application", "Persistence", "Infrastructure"];

    /// <summary>
    /// What both audit reports counted, independently of the table above. Pinned so that a wave
    /// which closes edges records how far the list has come from where it started.
    /// </summary>
    private const int CrossingsTheAuditCounted = 107;

    /// <summary>
    /// The three the audit's directive pattern could not see, because they are written as using
    /// aliases — <c>using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;</c> puts a
    /// name between <c>using</c> and the namespace, and the pattern was anchored on what follows
    /// <c>using</c>.
    /// </summary>
    private const int AliasedCrossingsTheAuditMissed = 3;

    /// <summary>
    /// The twenty-eight that are not directives at all.
    /// <para>
    /// <b>Nothing had counted these, including the first version of this rule.</b> They are written
    /// inline — <c>Modules.Media.Application.Assets.AttachMediaCommand</c>,
    /// <c>Location.Domain.ValueObjects.GeoCoordinate</c> — and inside a <c>Diaspora.Modules.*</c>
    /// namespace both short spellings compile, because C# resolves a namespace by walking outwards.
    /// So a reader auditing imports sees none of them, and neither did two measurements written to
    /// find exactly this.
    /// </para>
    /// <para>
    /// They are also not evenly spread: seven module → Media edges are <see cref="Reach.Application"/>
    /// entirely because of them, which is a different finding from the one the audit wrote down.
    /// </para>
    /// </summary>
    private const int InlineCrossingsNobodyCounted = 28;

    /// <summary>
    /// The ceiling the list may never exceed. A list that sums above this means crossings were
    /// added after the finding was raised, which is the thing the ratchet exists to prevent.
    /// </summary>
    private const int CrossingsFound =
        CrossingsTheAuditCounted + AliasedCrossingsTheAuditMissed + InlineCrossingsNobodyCounted;

    private static readonly Lazy<Crossings> Scanned = new(ScanTheModules);

    /// <summary>One reach into another module, and where it is written.</summary>
    private sealed record Crossing(string Consumer, string Producer, string Reached, string Where);

    /// <summary>
    /// What the scan found. <paramref name="Internal"/> is the crossings declared as using
    /// directives and <paramref name="InCode"/> the ones written inline with no directive to show
    /// for them; both are counted against the exemption list, and they are kept apart because the
    /// second kind is invisible to the eye and to every scan this platform had.
    /// </summary>
    private sealed record Crossings(
        List<Crossing> Internal,
        List<Crossing> InCode,
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
        var declared = EdgesStillOwed.ToDictionary(edge => (edge.Consumer, edge.Producer));

        var found = Scanned.Value.Internal.Select(crossing => (crossing, viaDirective: true))
            .Concat(Scanned.Value.InCode.Select(crossing => (crossing, viaDirective: false)))
            .GroupBy(entry => (entry.crossing.Consumer, entry.crossing.Producer))
            .ToDictionary(group => group.Key, group => group.ToList());

        var offenders = new List<string>();

        foreach (var (edge, crossings) in found.OrderBy(entry => entry.Key))
        {
            var directives = crossings.Count(entry => entry.viaDirective);
            var inCode = crossings.Count - directives;
            var where = string.Join(
                ", ", crossings.Select(entry => entry.crossing.Where).Order(StringComparer.Ordinal));

            if (!declared.TryGetValue(edge, out var allowed))
            {
                offenders.Add(
                    $"NEW EDGE {edge.Consumer} -> {edge.Producer} ({directives} directives, " +
                    $"{inCode} in code): reach {edge.Producer} through its Contracts, " +
                    "Extensibility or Integration namespace. If it genuinely cannot be reached " +
                    "that way, the seam is missing, and adding the edge to EdgesStillOwed is a " +
                    $"decision rather than a formality. {where}");

                continue;
            }

            if (directives != allowed.Directives || inCode != allowed.InCode)
            {
                var grew = directives + inCode > allowed.Crossings;

                offenders.Add(
                    (grew ? "GREW " : "SHRANK ") +
                    $"{edge.Consumer} -> {edge.Producer}: recorded " +
                    $"{allowed.Directives} directives and {allowed.InCode} in code, found " +
                    $"{directives} and {inCode}. " +
                    (grew
                        ? "Reach it through a published seam instead."
                        : "Decrement it in the commit that closed them, so the list stays the " +
                          "record of what is still owed.") +
                    $" {where}");
            }
        }

        foreach (var edge in declared.Keys.Except(found.Keys).OrderBy(edge => edge))
        {
            offenders.Add(
                $"CLOSED {edge.Consumer} -> {edge.Producer}: {declared[edge].Crossings} recorded, " +
                "none found. Remove the entry in the commit that closed the edge.");
        }

        offenders.Should().BeEmpty(
            "a module's Domain, Application and Persistence are its own; the list is a ratchet " +
            "and it has to be true in both directions to be one:\n" +
            string.Join("\n", offenders));
    }

    /// <summary>
    /// <b>A global using is counted once and reaches everywhere, so it is the one spelling the
    /// ratchet cannot measure.</b> Every other crossing is local: a directive opens one file and an
    /// inline name opens one expression, so counting them says something true about how much of a
    /// module depends on another. <c>global using</c> is assembly-scoped — one line in one file
    /// would let all 600 of a module's files name another module's aggregates, and the exemption
    /// list would record it as a 1.
    /// <para>
    /// The platform already uses global aliases deliberately —
    /// <c>Automation/Extensibility/Scopes/AutomationSeamAliases.cs</c> declares a dozen — and every
    /// one of them names <c>Diaspora.Contracts</c>, which is exactly right and exactly the reason
    /// to pin it before somebody reaches for the same trick against a module.
    /// </para>
    /// </summary>
    [Fact]
    public void A_global_using_never_opens_another_modules_internals()
    {
        var offenders = new List<string>();

        foreach (var file in SourceTree.ProductionFiles
            .Where(file => file.Path.StartsWith(ModuleSourcePrefix, StringComparison.Ordinal)))
        {
            var consumer = file.Path[ModuleSourcePrefix.Length..].Split('/')[0];

            offenders.AddRange(file.Root.DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .Where(directive => !directive.GlobalKeyword.IsKind(SyntaxKind.None))
                .Where(directive => TryReadCrossing(
                    directive.Name?.ToString(),
                    consumer,
                    Scanned.Value.Modules,
                    out _,
                    out var reached) && !IsPublished(reached))
                .Select(directive => $"{file.Locate(directive)}: {directive}"));
        }

        offenders.Should().BeEmpty(
            "a global using opens every file in the assembly at once, which is a dependency the " +
            "per-edge counts cannot describe:\n" + string.Join("\n", offenders));
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

        // The two spellings C# allows from inside Diaspora.Modules, and the reason this rule was
        // reporting a third of the truth.
        const string viaTheModulesPrefix = """
            namespace Diaspora.Modules.Housing.Probe;
            internal sealed class Probe
            {
                public object? Value => Modules.Location.Domain.Areas.Area.Create();
            }
            """;

        const string viaTheBareModuleName = """
            namespace Diaspora.Modules.Housing.Probe;
            internal sealed class Probe
            {
                public object? Value => Location.Domain.Areas.Area.Create();
            }
            """;

        // Neither of these is a reference: one is prose, the other is a module's own folder whose
        // first segment is not a module at all.
        const string inADocComment = """
            namespace Diaspora.Modules.Housing.Probe;
            /// <summary>See <c>Location.Domain.Areas.Area</c> for the shape.</summary>
            internal sealed class Probe { public int Value { get; set; } }
            """;

        const string aLocalNameThatLooksLikeOne = """
            namespace Diaspora.Modules.Housing.Probe;
            internal sealed class Probe
            {
                public object? Value => Domain.Listings.Listing.Create();
                public object? Other => Location.Resolve();
            }
            """;

        var byDirective = Probe(viaUsingDirective);
        byDirective.Internal.Should().ContainSingle(
            "the directive form is what most of the exemption list is written in");
        byDirective.InCode.Should().BeEmpty("the type is reached through the directive");

        var byAlias = Probe(viaAlias);
        byAlias.Internal.Should().ContainSingle(
            "an alias is a using directive, and three of the platform's crossings are written " +
            "this way — the form the audit's own measurement could not see");
        byAlias.InCode.Should().BeEmpty("the alias target is the directive's own name");

        var byStaticImport = Probe(viaStaticImport);
        byStaticImport.Internal.Should().ContainSingle(
            "a static import reaches a type the same as any other directive");

        var byFullName = Probe(viaFullName);
        byFullName.InCode.Should().ContainSingle(
            "a name written inline is a crossing with no directive to show for it");
        byFullName.Internal.Should().BeEmpty("there is no directive to attribute it to");

        var byModulesPrefix = Probe(viaTheModulesPrefix);
        byModulesPrefix.InCode.Should().ContainSingle(
            "Modules.Location binds to Diaspora.Modules.Location from inside a module, and 18 of " +
            "the platform's crossings are written exactly this way");

        var byBareName = Probe(viaTheBareModuleName);
        byBareName.InCode.Should().ContainSingle(
            "Location alone binds the same way one namespace further out, and 11 more are this");

        var bySeam = Probe(viaThePublishedSeam);
        bySeam.Internal.Should().BeEmpty("Extensibility is published");
        bySeam.InCode.Should().BeEmpty("Contracts is published, written in full or not");
        bySeam.Published.Should().HaveCount(2, "both spellings name a published namespace");

        var fromProse = Probe(inADocComment);
        fromProse.InCode.Should().BeEmpty(
            "a doc comment is trivia and DescendantNodes does not descend into it — without " +
            "which the short form would report a crossing for every <c>Module.Domain.Thing</c> " +
            "ever written in a summary");

        var fromALocalName = Probe(aLocalNameThatLooksLikeOne);
        fromALocalName.InCode.Should().BeEmpty(
            "Domain.Listings is the module's own folder and Location.Resolve() is a local " +
            "identifier whose second segment is not a module's folder: the short form needs both " +
            "halves of its guard, and this is the half that stops it over-matching");
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
            edge => edge.Crossings > 0, "an edge with no crossings is a closed edge");

        var known = Scanned.Value.Modules;

        EdgesStillOwed.SelectMany(edge => new[] { edge.Consumer, edge.Producer })
            .Distinct(StringComparer.Ordinal)
            .Should().OnlyContain(
                module => known.Contains(module),
                "an entry naming a module that does not exist exempts nothing. Modules: " +
                string.Join(", ", known.Order(StringComparer.Ordinal)));

        EdgesStillOwed.Sum(edge => edge.Crossings)
            .Should().BeLessThanOrEqualTo(
                CrossingsFound,
                "the list may shrink below the {0} measured here and must never exceed it: a " +
                "higher number means crossings were added after the finding was raised. The " +
                "audit's own figure was {1}, short by {2} written as using aliases and {3} " +
                "written inline with no directive at all",
                CrossingsFound,
                CrossingsTheAuditCounted,
                AliasedCrossingsTheAuditMissed,
                InlineCrossingsNobodyCounted);
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
        var reached = Scanned.Value.Internal.Concat(Scanned.Value.InCode)
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
    /// <b>A published read surface does not oblige a consumer to reach past it.</b>
    /// <para>
    /// The first wave's nine edges were not nine careless consumers. Every one was a module
    /// calling the seam it had been handed and finding an internal type in the signature:
    /// <c>IGuideDirectory</c> named B8's <c>GuideId</c>; <c>IProviderDirectory</c> named B7's
    /// <c>ProviderId</c> and <c>ProviderRole</c>. <b>A crossing of that kind cannot be closed
    /// where it is written</b>, and the list above was recording nine debts for two defects.
    /// </para>
    /// <para>
    /// So it is counted where it is owed. <c>Integration</c> is what a module publishes for other
    /// modules to <em>call</em>, and a reference from there into its own <c>Domain</c>,
    /// <c>Application</c>, <c>Persistence</c> or <c>Infrastructure</c> is a consumer's crossing
    /// with the consumer not yet written. The other two published segments are deliberately out
    /// of scope: <c>Extensibility</c> hands a contributor the module's own aggregate on purpose —
    /// a ranking strategy ranks a <c>Provider</c> — and <c>Contracts</c> is frozen payloads.
    /// </para>
    /// <para>
    /// Exact in both directions, for the reason the edge counts are: a surface that stops leaking
    /// and is not decremented here fails, so the number is what is owed today rather than what
    /// was owed in November. The numbers also predict the rest of the finding — the edges left
    /// above are overwhelmingly into Community and Location, and those are the two surfaces with
    /// the most left in them.
    /// </para>
    /// </summary>
    [Fact]
    public void A_published_read_surface_does_not_oblige_a_consumer_to_reach_past_it()
    {
        var declared = SurfacesStillLeaking.ToDictionary(
            debt => debt.Module, StringComparer.Ordinal);

        var found = ScanPublishedSurfaces();
        var offenders = new List<string>();

        foreach (var (module, references) in found.OrderBy(
            entry => entry.Key, StringComparer.Ordinal))
        {
            var where = string.Join(", ", references.Order(StringComparer.Ordinal));

            if (!declared.TryGetValue(module, out var allowed))
            {
                offenders.Add(
                    $"NEW LEAK {module}.Integration reaches its own internals {references.Count} " +
                    "time(s), so a consumer calling that seam is obliged to import them. Publish " +
                    $"the type, or take the primitive and convert inside {module}. {where}");

                continue;
            }

            if (references.Count != allowed.References)
            {
                var direction = references.Count > allowed.References ? "GREW" : "SHRANK";

                offenders.Add(
                    $"{direction} {module}.Integration: {allowed.References} recorded, " +
                    $"{references.Count} found. {where}");
            }
        }

        foreach (var debt in SurfacesStillLeaking.Where(debt => !found.ContainsKey(debt.Module)))
        {
            offenders.Add(
                $"CLOSED {debt.Module}.Integration: {debt.References} recorded, none found. " +
                "Remove the entry in the commit that cleaned the surface.");
        }

        offenders.Should().BeEmpty(
            "a published seam that names an internal type cannot be fixed at the call site:\n" +
            string.Join("\n", offenders));
    }

    /// <summary>
    /// <b>A contributor seed does not name the module's internals.</b>
    /// <para>
    /// A <c>*Seed</c> is the one published type a contributor <em>constructs</em> rather than
    /// receives, which is what separates it from the rest of <c>Extensibility</c> and from the
    /// reason <see cref="A_published_read_surface_does_not_oblige_a_consumer_to_reach_past_it"/>
    /// excludes that segment. A ranking strategy is handed a <c>Provider</c> on purpose; a module
    /// declaring a notification category has to fill one in, so every internal type in the seed is
    /// a crossing with the contributor not yet written.
    /// </para>
    /// <para>
    /// Added on the wave that closed the vocabulary class, because B13 was the case neither rule
    /// could see: <b>it has no <c>Integration</c> namespace</b>, so the surface rule had nothing to
    /// scan, and the three consumer edges were recorded against the three consumers while the
    /// obligation sat in <c>NotificationCategorySeed</c>. Six seeds, seven enums, sixteen files.
    /// </para>
    /// <para>
    /// Narrower than the exclusion it refines, deliberately. The aggregates in B13's surface stay:
    /// <c>TemplateRenderRequest</c> carries a <c>NotificationTemplate</c>,
    /// <c>IDigestGenerator</c> and <c>INotificationSearchEngine</c> name their aggregates, and all
    /// three are the case the exclusion was written for. The one adjacent case this rule does not
    /// cover is <c>IDeepLinkResolver</c>, which returns an internal <c>DeepLink</c> value object:
    /// no implementation outside B13 exists today, so nothing is obliged by it, and publishing a
    /// type with no consumer would be the shape this platform has been bitten by twice. It is
    /// written down here instead, and it becomes real the day a deployment ships its own resolver.
    /// </para>
    /// <para>
    /// Uses the same reader as the surface scan, so it sees a reach written relative to the module
    /// or with no module name at all — see
    /// <see cref="The_surface_scan_sees_an_internal_reach_however_it_is_written"/>.
    /// </para>
    /// </summary>
    [Fact]
    public void A_contributor_seed_does_not_name_the_modules_internals()
    {
        var declared = SeedsStillLeaking.ToDictionary(debt => debt.Module, StringComparer.Ordinal);
        var found = ScanContributorSeeds();
        var offenders = new List<string>();

        foreach (var (module, references) in found.OrderBy(
            entry => entry.Key, StringComparer.Ordinal))
        {
            var where = string.Join(", ", references.Order(StringComparer.Ordinal));

            if (!declared.TryGetValue(module, out var allowed))
            {
                offenders.Add(
                    $"NEW LEAK a {module} contributor seed names that module's own internals " +
                    $"{references.Count} time(s), so every contributor filling one in is obliged " +
                    $"to import them. Publish the type. {where}");

                continue;
            }

            if (references.Count != allowed.References)
            {
                var direction = references.Count > allowed.References ? "GREW" : "SHRANK";

                offenders.Add(
                    $"{direction} {module} seeds: {allowed.References} recorded, " +
                    $"{references.Count} found. {where}");
            }
        }

        foreach (var debt in SeedsStillLeaking.Where(debt => !found.ContainsKey(debt.Module)))
        {
            offenders.Add(
                $"CLOSED {debt.Module} seeds: {debt.References} recorded, none found. " +
                "Remove the entry in the commit that published the vocabulary.");
        }

        offenders.Should().BeEmpty(
            "a seed a contributor fills in cannot be filled in without the types it names:\n" +
            string.Join("\n", offenders));
    }

    /// <summary>
    /// <b>The surface scan sees an internal reach however it is written.</b>
    /// <para>
    /// The same probe the crossing scanner needed, for the same reason and against the same
    /// mistake. A scan of the published surfaces written as a regex over the fully qualified
    /// prefix reported Location as leaking 8 times; this one reports 10. The two it missed are
    /// <c>Domain.Enums.RadiusScope.Radius</c> in <c>GeographicScope</c> and
    /// <c>Domain.Enums.AdministrativeLevel</c> in <c>ILocationDirectory</c> — written with no
    /// module name at all, because from inside <c>Diaspora.Modules.Location.Integration</c> the
    /// walk outwards reaches <c>Diaspora.Modules.Location</c> and <c>Domain</c> binds there.
    /// </para>
    /// <para>
    /// That is the <see cref="InlineCrossingsNobodyCounted"/> lesson a second time, in a second
    /// scan, on the same afternoon the first one was being relied upon. The last case is the one
    /// that keeps the rule honest in the other direction: another module's internals are not this
    /// module's debt, and counting them here would charge Location for Community's leak.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("Diaspora.Modules.Location.Domain.ValueObjects", true, "fully qualified")]
    [InlineData("Modules.Location.Domain.ValueObjects", true, "relative to Diaspora")]
    [InlineData("Domain.Enums.AdministrativeLevel", true, "relative to the module itself")]
    [InlineData("Application.Areas.FindAreaQuery", true, "a layer other than Domain")]
    [InlineData("Diaspora.Modules.Community.Domain.ValueObjects", false, "another module's")]
    [InlineData("Diaspora.Contracts.Location.V1.Events", false, "published, and not a module's")]
    [InlineData("Diaspora.Modules.Location.Integration", false, "the surface's own namespace")]
    [InlineData("System.Collections.Generic", false, "not this platform's at all")]
    public void The_surface_scan_sees_an_internal_reach_however_it_is_written(
        string written, bool isDebt, string why)
    {
        TryReadOwnInternal(written, "Location", out var reached)
            .Should().Be(isDebt, "{0} is written {1}", written, why);

        if (isDebt)
        {
            reached.Should().NotBeEmpty("an unnamed reach cannot be chased to its statement");
        }
    }

    /// <summary>
    /// <b>A published seam is registered by the module that publishes it.</b>
    /// <para>
    /// A seam is only as real as its registration. An interface in <c>Integration</c> that no
    /// module registers compiles, satisfies every rule above, and fails at the first resolution —
    /// or worse, is quietly never resolved because nothing ever asked. This platform has met both
    /// shapes: a payment seam that was registered, tested and unrouted, and a circuit breaker with
    /// every piece present and nothing running under it.
    /// </para>
    /// <para>
    /// <b>The build already catches half of this, and it is worth knowing which half.</b> The
    /// OpenAPI document generator runs the API host during every build and composes the container
    /// with validation on, so a seam that something <em>injects</em> and nobody registers fails
    /// the build with <c>Unable to resolve service for type</c> — verified by removing this
    /// module's registration and watching it do exactly that.
    /// </para>
    /// <para>
    /// This rule is the other half: a seam published and registered nowhere that <em>nothing
    /// injects yet</em> compiles, builds clean and is simply dead until its first consumer
    /// arrives — and then fails in that consumer's PR rather than in the one that shipped the
    /// seam. Added with <c>IMediaAttachments</c>, which is the first seam written here for
    /// consumers that already existed.
    /// </para>
    /// <para>
    /// Matched on <c>&lt;IName&gt;</c> or <c>typeof(IName)</c> rather than on the name alone, so a
    /// mention in a comment is not mistaken for a registration.
    /// </para>
    /// </summary>
    [Fact]
    public void A_published_seam_is_registered_by_the_module_that_publishes_it()
    {
        var modules = SourceTree.ProductionFiles
            .Where(file => file.Path.StartsWith(ModuleSourcePrefix, StringComparison.Ordinal))
            .GroupBy(file => file.Path[ModuleSourcePrefix.Length..].Split('/')[0], StringComparer.Ordinal);

        var unregistered = new List<string>();
        var seams = 0;

        foreach (var module in modules)
        {
            var composition = module.FirstOrDefault(
                file => file.Path.EndsWith($"/{module.Key}Module.cs", StringComparison.Ordinal));

            if (composition is null)
            {
                continue;
            }

            var registrations = composition.Root.ToFullString();

            var published = module.Where(file =>
                file.Path[ModuleSourcePrefix.Length..].Split('/') is [_, "Integration", ..]);

            foreach (var declared in published
                .SelectMany(file => file.Root.DescendantNodes().OfType<InterfaceDeclarationSyntax>())
                .Where(declared => declared.Modifiers.Any(SyntaxKind.PublicKeyword))
                .Select(declared => declared.Identifier.Text))
            {
                seams++;

                if (!registrations.Contains($"<{declared}>", StringComparison.Ordinal)
                    && !registrations.Contains($"<{declared},", StringComparison.Ordinal)
                    && !registrations.Contains($"typeof({declared})", StringComparison.Ordinal))
                {
                    unregistered.Add(
                        $"{module.Key}.Integration.{declared} is published and " +
                        $"{module.Key}Module registers no implementation for it: a consumer that " +
                        "injects it cannot be constructed.");
                }
            }
        }

        seams.Should().BeGreaterThan(
            20, "twenty-four published seams were counted when this rule was written, and a scan " +
                "that finds almost none has stopped reading the Integration namespaces");

        unregistered.Should().BeEmpty(
            "a seam nothing registers is a seam nothing can use:\n" + string.Join("\n", unregistered));
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

        Scanned.Value.Internal.Should().BeEmpty(
            "the 110 crossings this rule shipped with are closed, so it ratchets the other way, " +
            "as the inline count already did on 20 November. A scan that finds none is no longer " +
            "evidence that it has stopped reading the source: " +
            nameof(Scanned.Value.Published) + " above is above a hundred and " +
            nameof(The_scanner_sees_a_crossing_however_it_is_written) + " runs source written " +
            "for the purpose through this same reader, which together say the scan works and the " +
            "platform is clean rather than the scan being broken");

        Scanned.Value.InCode.Should().BeEmpty(
            "all 28 inline crossings are closed, so this now ratchets the other way: a crossing " +
            "written inline is one a reader of the imports cannot see, and the next one to appear " +
            "fails here rather than waiting to be discovered by a scan written to look for it. " +
            "That this rule can still see one is proved by " +
            nameof(The_scanner_sees_a_crossing_however_it_is_written) + ", on source written for " +
            "the purpose — which is the only evidence available once the production population is " +
            "empty, and better evidence than a count that happened to be above zero");
    }

    // ── The scan ─────────────────────────────────────────────────────────

    private static Crossings ScanTheModules()
    {
        var files = SourceTree.ProductionFiles
            .Where(file => file.Path.StartsWith(ModuleSourcePrefix, StringComparison.Ordinal))
            .ToList();

        // The module names are needed before the first file is read, not accumulated as it goes:
        // resolving the short form below asks "is this first segment a module?", and a set built
        // in scan order would answer no for every module the walk has not reached yet.
        var modules = files
            .Select(file => file.Path[ModuleSourcePrefix.Length..].Split('/')[0])
            .ToHashSet(StringComparer.Ordinal);

        var crossings = new Crossings([], [], [], modules, files.Count);

        foreach (var file in files)
        {
            ReadOneFile(file, file.Path[ModuleSourcePrefix.Length..].Split('/')[0], crossings);
        }

        return crossings;
    }

    /// <summary>
    /// Every reference from a module's <c>Integration</c> namespace into its own internals, by
    /// module, with where each one is written.
    /// <para>
    /// Both spellings, for the reason <see cref="ReadOneFile"/> counts both. Inside
    /// <c>Diaspora.Modules.Providers.Integration</c> the walk outwards reaches
    /// <c>Diaspora.Modules.Providers</c>, so <c>Domain.Enums.ProviderRole</c> compiles with no
    /// import naming it — and that spelling is what three files in B7 used, which a scan looking
    /// only for the fully qualified form would have called clean.
    /// </para>
    /// </summary>
    private static Dictionary<string, List<string>> ScanPublishedSurfaces() =>
        ScanSurfaces(static (_, segments) =>
            segments.Length >= 2
            && string.Equals(segments[1], "Integration", StringComparison.Ordinal));

    /// <summary>
    /// The seeds a contributor constructs, which is the part of <c>Extensibility</c> that
    /// obliges rather than hands over. Named by convention — <c>*Seed</c> is what this platform
    /// has called them since B13's catalogue shipped — so a seed named something else is outside
    /// this rule, and the naming test in the contributor suites is what keeps that honest.
    /// </summary>
    private static Dictionary<string, List<string>> ScanContributorSeeds() =>
        ScanSurfaces(static (path, segments) =>
            segments.Length >= 2
            && string.Equals(segments[1], "Extensibility", StringComparison.Ordinal)
            && path.EndsWith("Seed.cs", StringComparison.Ordinal));

    private static Dictionary<string, List<string>> ScanSurfaces(
        Func<string, string[], bool> include)
    {
        var found = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var file in SourceTree.ProductionFiles)
        {
            if (!file.Path.StartsWith(ModuleSourcePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var segments = file.Path[ModuleSourcePrefix.Length..].Split('/');

            if (!include(file.Path, segments))
            {
                continue;
            }

            var module = segments[0];

            foreach (var node in file.Root.DescendantNodes())
            {
                var written = node switch
                {
                    UsingDirectiveSyntax directive => directive.Name?.ToString(),
                    QualifiedNameSyntax or MemberAccessExpressionSyntax
                        when node.Parent is not (QualifiedNameSyntax
                            or MemberAccessExpressionSyntax
                            or UsingDirectiveSyntax
                            or NamespaceDeclarationSyntax
                            or FileScopedNamespaceDeclarationSyntax) => Unspaced(node.ToString()),
                    _ => null
                };

                if (!TryReadOwnInternal(written, module, out var reached))
                {
                    continue;
                }

                if (!found.TryGetValue(module, out var references))
                {
                    references = [];
                    found[module] = references;
                }

                references.Add($"{file.Locate(node)} reaches {reached}");
            }
        }

        return found;
    }

    /// <summary>
    /// Whether <paramref name="written"/> names this module's own internals, in any of the
    /// spellings that compile from inside its <c>Integration</c> namespace.
    /// </summary>
    private static bool TryReadOwnInternal(string? written, string module, out string reached)
    {
        reached = string.Empty;

        if (string.IsNullOrEmpty(written))
        {
            return false;
        }

        var qualified = $"{ModulePrefix}{module}.";
        var relative = $"{RelativeModulePrefix}{module}.";

        var tail =
            written.StartsWith(qualified, StringComparison.Ordinal) ? written[qualified.Length..]
            : written.StartsWith(relative, StringComparison.Ordinal) ? written[relative.Length..]
            : written;

        if (!InnerSegments.Contains(tail.Split('.')[0], StringComparer.Ordinal))
        {
            return false;
        }

        reached = tail;

        return true;
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

            if (!TryReadCrossing(named, consumer, into.Modules, out var producer, out var reached))
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

            if (!TryReadCrossing(written, consumer, into.Modules, out var producer, out var reached))
            {
                continue;
            }

            var crossing = new Crossing(consumer, producer, reached, file.Locate(node));

            if (IsPublished(reached))
            {
                into.Published.Add(crossing);
                continue;
            }

            // A name whose namespace this file already imports is the directive's crossing written
            // out again — to disambiguate a collision, usually — not a second one, and the
            // directive is already recorded. Compared in canonical form, because the name in code
            // may be written relatively and the directive never is.
            var canonical = $"{ModulePrefix}{producer}.{reached}";

            var alreadyImported = imported.Any(namespaceName =>
                string.Equals(canonical, namespaceName, StringComparison.Ordinal) ||
                canonical.StartsWith(namespaceName + ".", StringComparison.Ordinal));

            if (!alreadyImported)
            {
                into.InCode.Add(crossing);
            }
        }
    }

    /// <summary>
    /// Resolves a written name to the module it reaches and the namespace inside it, rejecting
    /// anything that is not another module's.
    /// <para>
    /// <b>Three spellings, because C# resolves a namespace by walking outwards and all three
    /// compile.</b> Inside <c>Diaspora.Modules.Community.Application.Services</c>, the identifier
    /// <c>Modules</c> finds no member until the walk reaches <c>Diaspora</c>, where it binds to
    /// <c>Diaspora.Modules</c>; one step further out, <c>Location</c> binds to
    /// <c>Diaspora.Modules.Location</c> the same way. So
    /// <c>Diaspora.Modules.Location.Domain.ValueObjects.GeoCoordinate</c>,
    /// <c>Modules.Location.Domain.…</c> and <c>Location.Domain.…</c> are one dependency written
    /// three ways, and the short two are the ones an editor offers.
    /// </para>
    /// <para>
    /// <b>All three are in use, and reading only the first is how 28 crossings stayed invisible</b>
    /// — to the audit, and to the first version of this rule. <c>FeedComposer</c> reaches
    /// <c>GeoCoordinate</c> four times with no directive to show for it, and six modules construct
    /// <c>Modules.Media.Application.Assets.AttachMediaCommand</c>, which is an application-layer
    /// reach that nothing had counted.
    /// </para>
    /// <para>
    /// The short form is the only one that needs a guard, since its first segment is an ordinary
    /// identifier: it must name a module, and the segment after it must be one of a module's own
    /// folders. A doc comment writing <c>Community.Domain.Polls.Poll</c> is not a reference and is
    /// excluded already, because <c>DescendantNodes()</c> does not descend into trivia.
    /// </para>
    /// </summary>
    private static bool TryReadCrossing(
        string? named,
        string consumer,
        HashSet<string> modules,
        out string producer,
        out string reached)
    {
        producer = string.Empty;
        reached = string.Empty;

        if (named is null)
        {
            return false;
        }

        string[] segments;

        if (named.StartsWith(ModulePrefix, StringComparison.Ordinal))
        {
            segments = named[ModulePrefix.Length..].Split('.');
        }
        else if (named.StartsWith(RelativeModulePrefix, StringComparison.Ordinal))
        {
            segments = named[RelativeModulePrefix.Length..].Split('.');
        }
        else
        {
            segments = named.Split('.');

            if (segments.Length < 2 ||
                !modules.Contains(segments[0]) ||
                !IsAModulesOwnFolder(segments[1]))
            {
                return false;
            }
        }

        // "Diaspora.Modules.Location" on its own names no namespace inside the module.
        if (segments.Length < 2)
        {
            return false;
        }

        producer = segments[0];

        if (string.Equals(producer, consumer, StringComparison.Ordinal))
        {
            producer = string.Empty;

            return false;
        }

        reached = string.Join('.', segments.Skip(1));

        return true;
    }

    /// <summary>
    /// Whether a segment is one of the folders every module has, which is what tells the short
    /// form apart from an ordinary dotted expression whose first name happens to match a module.
    /// </summary>
    private static bool IsAModulesOwnFolder(string segment) =>
        PublishedSegments.Contains(segment, StringComparer.Ordinal) ||
        LayerSegments.Contains(segment, StringComparer.Ordinal) ||
        string.Equals(segment, "Domain", StringComparison.Ordinal) ||
        string.Equals(segment, "Catalog", StringComparison.Ordinal) ||
        string.Equals(segment, "Api", StringComparison.Ordinal);

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

        // Every probe is written as if it were a Housing file reaching Location. The module set is
        // supplied rather than scanned, because the short form resolves against it.
        var crossings = new Crossings(
            [], [], [], ["Housing", "Location", "Community", "Media"], 0);

        ReadOneFile(file, "Housing", crossings);

        return crossings;
    }
}
