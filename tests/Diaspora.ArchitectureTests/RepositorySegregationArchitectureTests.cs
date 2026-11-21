using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// BSOLID-004. A repository interface is a contract with the aggregate, not a cupboard.
/// <para>
/// The finding measured 619 interfaces and found the distribution healthy with a long tail: a
/// handful over eighteen members, mixing identity-based access with search, guards and reporting.
/// What made it a real defect rather than a size complaint was the usage: the heaviest consumer of
/// the 24-member <c>IHousingListingRepository</c> used <b>four</b> of them, and 214 consumers
/// across the tail used exactly one.
/// </para>
/// <para>
/// <b>The finding's three verification criteria do not agree with each other.</b> It asks for no
/// <c>I*Repository</c> over twelve members, the largest split, and no handler taking a repository
/// it uses one method of. The third is not reachable by the two-way split the remediation
/// describes — splitting 24 into 8 writes and 16 queries still hands a consumer that needs one
/// count a 16-member dependency; only a role per reason-to-change gets there, which is what the
/// split interfaces are, and <see cref="No_holder_takes_a_role_wider_than_it_uses"/> is its
/// enforceable form.
/// </para>
/// <para>
/// <b>The first criterion is right and the remediation beside it is wrong, which took a
/// measurement to establish.</b> The remediation says to leave the 13–14 band alone unless it is
/// being touched anyway. Measured on 21 November 2026, that band has the same defect the tail had:
/// <b>190 of its 295 consumers use exactly one member</b>, 258 use two or fewer, and the heaviest
/// consumer of the 69-consumer <c>IGuideRepository</c> uses three of its fourteen. Only
/// <c>ICommunityInitiativeRepository</c> is genuinely cohesive — two consumers, one of which uses
/// all fourteen. The band is smaller bags rather than smaller contracts, so the list below is what
/// remains to be split, not debt anybody has agreed to keep.
/// </para>
/// </summary>
public sealed class RepositorySegregationArchitectureTests
{
    /// <summary>
    /// The repository interfaces that are still over the finding's ceiling, with their exact
    /// member counts.
    /// <para>
    /// <b>Exact in both directions.</b> A new entry fails, a changed count fails, and so does
    /// removing one without editing this list — which is how a split gets recorded rather than
    /// merely happening. Housing, Marketplace and Providers left this list on 21 November 2026.
    /// </para>
    /// <para>
    /// Not a to-do list with a promise attached. The finding's own remediation says to leave the
    /// 13–14 band alone unless it is being touched anyway, and fourteen of these eighteen are in
    /// it. What this list does is stop the tail growing while nobody is looking, and make the next
    /// split a deliberate edit.
    /// </para>
    /// </summary>
    private static readonly (string Interface, int Members)[] RepositoriesStillOverTwelve =
    [
        ("IAcademyRepository", 14),
        ("IAdministrativeAreaRepository", 14),
        ("ICommunityInitiativeRepository", 14),
        ("ICommunityPollRepository", 14),
        ("IGuideRepository", 14),
        ("IJobPostingRepository", 14),
        ("INotificationRepository", 14),
        ("IProgrammeMembershipRepository", 14),
        ("IAnalyticsRollupRepository", 13),
        ("ICommunityPostRepository", 13),
        ("IConversationRepository", 13),
        ("IEventRsvpRepository", 13),
        ("IOrganizationRepository", 13),
        ("IPlacementRepository", 13),
    ];

    /// <summary>
    /// The role interfaces Housing, Marketplace and Providers were split into, by family.
    /// <para>
    /// A member may be declared on two roles of one family and that is deliberate:
    /// <c>GetManyAsync</c> is a batch identity load <em>and</em> the hydration step of a search,
    /// so a caller that only hydrates should not have to take the write side to do it. One
    /// implementation satisfies both declarations.
    /// </para>
    /// </summary>
    private static readonly string[][] Families =
    [
        ["IHousingListingRepository", "IHousingListingSearch", "IHousingListingGuards",
            "IHousingListingStatistics"],
        ["IMarketplaceListingRepository", "IMarketplaceListingSearch",
            "IMarketplaceListingSellerFacts", "IMarketplaceListingStatistics"],
        ["IProviderRepository", "IProviderSearch", "IProviderMembership",
            "IProviderStatistics"],
        ["ICommunityEventRepository", "ICommunityEventSearch", "ICommunityEventGuards",
            "ICommunityEventSeries", "ICommunityEventStatistics"],
        ["IAnalyticsEventRepository", "IAnalyticsCellSource", "IAnalyticsEventReports",
            "IAnalyticsEventHousekeeping"],
        ["ICreatorRewardRepository", "ICreatorPointRuleRepository",
            "ICreatorRewardRedemptionRepository"],
        ["IJobApplicationRepository", "IJobApplicationSearch", "IJobApplicationFacts"],
    ];

    private const int Ceiling = 12;

    // ── The tail, recorded exactly ───────────────────────────────────────

    /// <summary>
    /// Every repository interface over the ceiling is on the list, at the count it is on it.
    /// </summary>
    [Fact]
    public void The_repositories_over_the_ceiling_are_the_ones_recorded()
    {
        var found = InterfaceMembers()
            .Where(pair => pair.Key.StartsWith('I') && pair.Key.EndsWith("Repository", StringComparison.Ordinal))
            .Where(pair => pair.Value > Ceiling)
            .Select(pair => (pair.Key, pair.Value))
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .ToList();

        found.Should().Equal(
            RepositoriesStillOverTwelve,
            "the tail is recorded exactly, so it cannot grow quietly and a split cannot land " +
            "without this list saying so. Found: {0}",
            string.Join(", ", found.Select(pair => $"{pair.Item1}={pair.Item2}")));
    }

    /// <summary>
    /// The sanity check every source-reading rule needs: a scan that found no repositories at all
    /// would satisfy the rule above only because its own population was empty.
    /// </summary>
    [Fact]
    public void The_scan_read_the_platforms_repositories()
    {
        var repositories = InterfaceMembers()
            .Count(pair => pair.Key.StartsWith('I')
                && pair.Key.EndsWith("Repository", StringComparison.Ordinal));

        repositories.Should().BeGreaterThan(
            200, "the platform declares a repository interface per aggregate and there are many");
    }

    // ── The split families ───────────────────────────────────────────────

    /// <summary>
    /// Every role of a split family exists and is inside the ceiling.
    /// <para>
    /// The point of the split is that no consumer has to take twenty-four methods to ask one
    /// question; a role that drifted back over the ceiling would have given that up.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_split_role_stays_inside_the_ceiling()
    {
        var members = InterfaceMembers();

        var oversized = new List<string>();
        foreach (var role in Families.SelectMany(family => family))
        {
            members.Should().ContainKey(
                role, "{0} is a declared role of a split repository family", role);

            if (members[role] > Ceiling)
            {
                oversized.Add($"{role}={members[role]}");
            }
        }

        oversized.Should().BeEmpty(
            "a role back over {0} members has stopped being a role. Offenders: {1}",
            Ceiling, string.Join(", ", oversized));
    }

    /// <summary>
    /// <b>The achievable form of the finding's third criterion.</b> No holder of a split role uses
    /// so little of it that a narrower role of the same family would have done.
    /// <para>
    /// This is the one that keeps the split honest as the code changes. A handler that loses its
    /// last write and keeps injecting the write interface is back to depending on what it does not
    /// use, and nothing else would notice — the build is happy, the tests pass, and the dependency
    /// is simply wrong.
    /// </para>
    /// <para>
    /// Fields only, deliberately. A local resolved from a scope inside a sweep is bound and used
    /// within a few lines where the mismatch is visible; a constructor-injected field is the one
    /// that outlives the reason it was added.
    /// </para>
    /// </summary>
    [Fact]
    public void No_holder_takes_a_role_wider_than_it_uses()
    {
        var byFamily = Families
            .SelectMany(family => family.Select(role => (role, family)))
            .ToDictionary(pair => pair.role, pair => pair.family, StringComparer.Ordinal);

        var declared = DeclaredMembers();
        var offenders = new List<string>();
        var holders = 0;

        foreach (var file in SourceTree.ProductionFiles.Concat(TestFiles()))
        {
            foreach (var field in file.Root.DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                var type = field.Declaration.Type.ToString();
                if (!byFamily.TryGetValue(type, out var family))
                {
                    continue;
                }

                holders++;

                var name = field.Declaration.Variables[0].Identifier.ValueText;
                var used = file.Root.DescendantNodes()
                    .OfType<MemberAccessExpressionSyntax>()
                    .Where(access => access.Expression is IdentifierNameSyntax identifier
                        && identifier.Identifier.ValueText == name)
                    .Select(access => access.Name.Identifier.ValueText)
                    .ToHashSet(StringComparer.Ordinal);

                if (used.Count == 0)
                {
                    continue;
                }

                var narrower = family
                    .Where(role => role != type && declared[role].Count < declared[type].Count)
                    .Where(role => used.IsSubsetOf(declared[role]))
                    .OrderBy(role => declared[role].Count)
                    .FirstOrDefault();

                if (narrower is not null)
                {
                    offenders.Add($"{file.Path}: {name} holds {type} but {narrower} covers " +
                                  $"{string.Join(", ", used.Order(StringComparer.Ordinal))}");
                }
            }
        }

        holders.Should().BeGreaterThan(
            50, "the split families are injected widely; finding almost none means this rule is " +
                "reading the wrong thing rather than that the platform is clean");

        offenders.Should().BeEmpty(
            "a role is the set of questions a consumer actually asks. Offenders: {0}",
            string.Join(" | ", offenders));
    }

    // ── Reading the source ───────────────────────────────────────────────

    private static Dictionary<string, int> InterfaceMembers() =>
        SourceTree.ProductionFiles
            .SelectMany(file => file.Root.DescendantNodes().OfType<InterfaceDeclarationSyntax>())
            .GroupBy(declaration => declaration.Identifier.ValueText, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Max(d => d.Members.Count),
                StringComparer.Ordinal);

    private static Dictionary<string, HashSet<string>> DeclaredMembers() =>
        SourceTree.ProductionFiles
            .SelectMany(file => file.Root.DescendantNodes().OfType<InterfaceDeclarationSyntax>())
            .GroupBy(declaration => declaration.Identifier.ValueText, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.SelectMany(Names).ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);

    private static IEnumerable<string> Names(InterfaceDeclarationSyntax declaration) =>
        declaration.Members.Select(member => member switch
        {
            MethodDeclarationSyntax method => method.Identifier.ValueText,
            PropertyDeclarationSyntax property => property.Identifier.ValueText,
            _ => string.Empty
        }).Where(name => name.Length > 0);

    /// <summary>
    /// The test tree, read the same way as the production tree. The suites resolve these roles
    /// too, and a scale test holding the write interface to run a search is the same mistake.
    /// </summary>
    private static IEnumerable<SourceFile> TestFiles()
    {
        var tests = new DirectoryInfo(Path.Combine(SourceTree.Root.FullName, "tests"));

        return tests
            .EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(file => !file.FullName.Replace('\\', '/').Contains("/obj/", StringComparison.Ordinal))
            .Where(file => !file.FullName.Replace('\\', '/').Contains("/bin/", StringComparison.Ordinal))
            .Select(file => new SourceFile(
                Path.GetRelativePath(SourceTree.Root.FullName, file.FullName).Replace('\\', '/'),
                Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree
                    .ParseText(File.ReadAllText(file.FullName)).GetRoot()));
    }
}
