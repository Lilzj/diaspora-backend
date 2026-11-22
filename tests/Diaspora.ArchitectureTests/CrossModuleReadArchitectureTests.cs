using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// BARCH-005's open half: every loop that crosses a module boundary once per iteration, recorded.
/// <para>
/// The finding's first half fixed two N+1 reads and guarded their two methods by name.
/// <see cref="HotReadPathArchitectureTests"/> says why a named list rather than the platform: a
/// scan for an awaited data call inside a <c>foreach</c> returns 77 sites, most of them sweeps
/// where sequential work is correct, and a rule that fails on all of them is switched off within a
/// day. What the finding asks for beyond those two is that <b>every page-composing cross-module
/// port</b> be checked for the same shape, and that needs a population narrow enough to be a rule
/// and wide enough to be an answer.
/// </para>
/// <para>
/// <b>The population is the crossing, not the loop.</b> A sweep looping over its own repository is
/// one connection doing sequential work. A loop that awaits another module's published seam pays a
/// boundary per row, and that is the cost BBL-005 measured at fifty round trips before a post
/// could be ranked. Narrowing to published seams — <c>Integration</c>, <c>Extensibility</c> and
/// the platform's service abstractions — and to members that are actually asynchronous takes 77
/// candidates down to the thirteen below, which is small enough to classify by hand and did not
/// need a judgement call to produce.
/// </para>
/// <para>
/// <b>None of the ten is a page composition, and that is the answer to the finding.</b> They are
/// fan-outs, seeders, payments, search indexing and two bounded cursor walks whose bounds are
/// documented where they are written. Recording them exactly is what makes the check standing
/// rather than a thing somebody did once: an eleventh has to be classified before the build goes
/// green.
/// </para>
/// <para>
/// <b>A text scan of the same question reported an eleventh that is not one.</b>
/// <c>CampaignEndpoints</c> registers six lifecycle routes in a loop, and a regular expression
/// sees a loop whose body contains <c>await campaigns.TransitionAsync</c> — but the await belongs
/// to the handler lambda being registered, and runs once per request rather than once per
/// iteration. The syntax tree distinguishes them because it knows where the lambda begins. Worth
/// recording because the text scan came first and was wrong in the direction that invents work.
/// </para>
/// </summary>
public sealed class CrossModuleReadArchitectureTests
{
    /// <param name="File">The file the loop is in.</param>
    /// <param name="Seam">The published member it awaits once per iteration.</param>
    /// <param name="Why">Why sequential is right here — the thing a new entry has to supply.</param>
    private sealed record Crossing(string File, string Seam, string Why);

    private static readonly Crossing[] CrossingLoops =
    [
        new("src/Diaspora.Modules.CommunityLife/Application/Discovery/SearchEventsQueryHandler.cs",
            "ICommunityEventSearchEngine.SearchAsync",
            "Nearest reads at most five pages of candidates because the database has no distance "
            + "to sort by. Bounded by a named constant, documented where it is written, and "
            + "budgeted by EndpointQueryBudgetTests"),

        new("src/Diaspora.Modules.CommunityProgramme/Api/CampaignEndpoints.cs",
            "ICampaigns.SearchAsync",
            "an organiser's own campaigns, bounded by a named constant with the comment "
            + "'an organiser with more than this is paging, and should use the search'"),

        new("src/Diaspora.Modules.Conversations/Realtime/ConversationBroadcaster.cs",
            "IRealtimeNotifier.SendToUserAsync",
            "a realtime fan-out: one push per recipient is the work, not an accident of shape, "
            + "and there is no plural form to batch into"),

        new("src/Diaspora.Modules.Creator/Application/Earnings/CreatorEarningClearing.cs",
            "IPlatformDisbursement.PayAsync",
            "one payment per creator. Sequential is required rather than tolerated: a batch that "
            + "half-succeeded would be money in an unknown state"),

        new("src/Diaspora.Modules.Integrations/Application/Sync/SyncHandlers.cs",
            "IExternalCalendarProvider.FindEventAsync",
            "one call per event to somebody else's calendar API. The round trip is the provider's, "
            + "not this database's, and the provider has no batch endpoint"),

        new("src/Diaspora.Modules.Location/Persistence/Seeding/OsmBoundarySeeder.cs",
            "ILocationCatalogWriter.EnsureBoundaryAsync",
            "a seeder. Sequential work on a background path with no request waiting on it"),

        new("src/Diaspora.Modules.Location/Persistence/Seeding/WorldCountrySeeder.cs",
            "ILocationCatalogWriter.EnsureCountryAsync",
            "the same, for countries: a seeder on a background path with no request waiting"),

        new("src/Diaspora.Modules.Marketplace/Application/EventHandlers/SellerEventHandler.cs",
            "IMarketplaceSearchEngine.IndexAsync",
            "re-indexing a seller's listings after a profile change, on an event handler rather "
            + "than a request"),

        new("src/Diaspora.Modules.Marketplace/Application/EventHandlers/SellerEventHandler.cs",
            "IMarketplaceSearchEngine.RemoveAsync",
            "the same loop's other arm: a listing leaves the index when its seller is hidden"),

        new("src/Diaspora.Modules.Notifications/Realtime/RealtimeConnectionCoordinator.cs",
            "IRealtimeGroupAuthorizer.CanObservePresenceAsync",
            "one authorization question per connection joining a group. The answer is per "
            + "connection and there is nothing to batch"),
    ];

    private static readonly string[] PublishedNamespaces =
        ["/Integration/", "/Extensibility/", "Diaspora.Platform/Services/Abstractions/"];

    /// <summary>
    /// The loops that cross a module boundary once per iteration are exactly the ones recorded.
    /// </summary>
    [Fact]
    public void Every_loop_that_crosses_a_module_boundary_per_row_is_accounted_for()
    {
        var seams = PublishedSeams();
        var found = new List<string>();

        foreach (var file in SourceTree.ProductionFiles)
        {
            var holders = Holders(file, seams);

            if (holders.Count == 0)
            {
                continue;
            }

            foreach (var loop in file.Root.DescendantNodes().Where(IsLoop))
            {
                foreach (var call in loop.DescendantNodes().OfType<AwaitExpressionSyntax>()
                             .SelectMany(await => await.DescendantNodes()
                                 .OfType<MemberAccessExpressionSyntax>()))
                {
                    if (call.Expression is not IdentifierNameSyntax holder
                        || !holders.TryGetValue(holder.Identifier.ValueText, out var seam))
                    {
                        continue;
                    }

                    var member = call.Name.Identifier.ValueText;

                    if (!seams[seam].Contains(member))
                    {
                        continue;
                    }

                    found.Add($"{file.Path}|{seam}.{member}");
                }
            }
        }

        var recorded = CrossingLoops
            .Select(crossing => $"{crossing.File}|{crossing.Seam}")
            .ToHashSet(StringComparer.Ordinal);

        var distinct = found.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        distinct.Except(recorded, StringComparer.Ordinal).Should().BeEmpty(
            "a loop that awaits another module's seam pays a boundary per row, which is what " +
            "BBL-005 measured at fifty round trips. A new one is either a page composition — the " +
            "defect — or something sequential for a reason, and the reason belongs in " +
            "CrossingLoops where the next reader can see it. Found: {0}",
            string.Join(" / ", distinct));

        var stale = recorded.Except(distinct, StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .ToList();

        stale.Should().BeEmpty(
            "a recorded crossing the scan no longer finds has been fixed or moved, and the entry " +
            "should go with it rather than sit here describing code that is not there. Stale: {0}",
            string.Join(" / ", stale));
    }

    /// <summary>
    /// Every recorded crossing names a file that exists and says why sequential is right.
    /// <para>
    /// An entry whose file was renamed stops guarding anything and says nothing about it, which is
    /// how a recorded list becomes decoration.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_recorded_crossing_still_exists_and_explains_itself()
    {
        var missing = CrossingLoops
            .Where(crossing => !File.Exists(
                Path.Combine(SourceTree.Root.FullName, crossing.File)))
            .Select(crossing => crossing.File)
            .ToList();

        missing.Should().BeEmpty(
            "a recorded crossing whose file is gone guards nothing. Missing: {0}",
            string.Join(", ", missing));

        CrossingLoops.Where(crossing => crossing.Why.Length < 40)
            .Select(crossing => $"{crossing.File}: {crossing.Seam}")
            .Should().BeEmpty(
                "the reason is the entry's whole value — without it the list records that " +
                "somebody looked, not what they concluded");
    }

    /// <summary>
    /// The sanity check: a scan that found no published seams would satisfy the rule above only
    /// because it had nothing to look for.
    /// </summary>
    [Fact]
    public void The_scan_read_the_platforms_published_seams()
    {
        var seams = PublishedSeams();

        seams.Should().HaveCountGreaterThan(
            100, "the platform publishes many seams across Integration, Extensibility and the " +
                 "service abstractions");

        seams.Values.Sum(members => members.Count).Should().BeGreaterThan(
            200, "and those seams declare many asynchronous members");
    }

    // ── Reading the source ───────────────────────────────────────────────

    private static bool IsLoop(SyntaxNode node) =>
        node is ForEachStatementSyntax or ForStatementSyntax or WhileStatementSyntax
            or DoStatementSyntax;

    /// <summary>Every published seam, and the asynchronous members it declares.</summary>
    private static Dictionary<string, HashSet<string>> PublishedSeams()
    {
        var seams = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var file in SourceTree.ProductionFiles)
        {
            if (!PublishedNamespaces.Any(
                    part => ("/" + file.Path).Contains(part, StringComparison.Ordinal)))
            {
                continue;
            }

            foreach (var declaration in file.Root.DescendantNodes()
                         .OfType<InterfaceDeclarationSyntax>())
            {
                var members = declaration.Members
                    .OfType<MethodDeclarationSyntax>()
                    .Select(method => method.Identifier.ValueText)
                    .Where(name => name.EndsWith("Async", StringComparison.Ordinal))
                    .ToHashSet(StringComparer.Ordinal);

                if (members.Count > 0)
                {
                    seams[declaration.Identifier.ValueText] = members;
                }
            }
        }

        return seams;
    }

    /// <summary>Identifiers in a file bound to a published seam.</summary>
    private static Dictionary<string, string> Holders(
        SourceFile file, Dictionary<string, HashSet<string>> seams)
    {
        var holders = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var field in file.Root.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            if (seams.ContainsKey(field.Declaration.Type.ToString()))
            {
                holders[field.Declaration.Variables[0].Identifier.ValueText] =
                    field.Declaration.Type.ToString();
            }
        }

        foreach (var parameter in file.Root.DescendantNodes().OfType<ParameterSyntax>())
        {
            if (parameter.Type is not null && seams.ContainsKey(parameter.Type.ToString()))
            {
                holders[parameter.Identifier.ValueText] = parameter.Type.ToString();
            }
        }

        return holders;
    }
}
