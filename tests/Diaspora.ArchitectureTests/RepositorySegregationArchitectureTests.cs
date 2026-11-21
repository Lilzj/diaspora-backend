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
/// <b>The finding's three verification criteria did not agree with each other.</b> It asks for no
/// <c>I*Repository</c> over twelve members, the largest split, and no handler taking a repository
/// it uses one method of. The third is not reachable by the two-way split the remediation
/// describes — splitting 24 into 8 writes and 16 queries still hands a consumer that needs one
/// count a 16-member dependency; only a role per reason-to-change gets there, which is what the
/// roles below are, and <see cref="No_holder_takes_a_role_wider_than_it_uses"/> is its enforceable
/// form. All three hold now: 228 repository interfaces, none over twelve.
/// </para>
/// <para>
/// <b>The first criterion was right and the remediation beside it was wrong, which took a
/// measurement to establish — and then a second measurement to get right.</b> The remediation says
/// to leave the 13–14 band alone unless it is being touched anyway. A file-scoped scan of that
/// band reported 295 consumers, 190 of them using exactly one member, and exactly one interface
/// that looked cohesive. Re-measured per <em>class</em> on 21 November 2026 — because a file
/// holding thirteen handlers that share a field name reads as one consumer using the union of
/// everything they call — the band had <b>340 consumer classes, 228 of them using exactly one
/// member</b>, 303 using two or fewer, and <b>no cohesive interface at all</b>: the heaviest
/// consumer anywhere in the band used seven of thirteen, and the apparently cohesive
/// <c>ICommunityInitiativeRepository</c> turned out to have nine consumers, six of which used one
/// member each. A file-scoped scan undercounts consumers and overstates what each one uses, and
/// both errors point the same way: towards leaving a bag alone.
/// </para>
/// <para>
/// Two interfaces over twelve members remain on the platform and are deliberately out of scope:
/// <c>IMediaStorageProvider</c> at fourteen is a provider contract an implementor satisfies whole,
/// and <c>ICampaigns</c> at thirteen is a cross-module integration façade. Neither is a
/// repository, which is what this finding measured.
/// </para>
/// </summary>
public sealed class RepositorySegregationArchitectureTests
{
    /// <summary>
    /// The role interfaces the oversized repositories were split into, by family.
    /// <para>
    /// Twenty-one families, sixty-nine roles, none over eleven members. A member may be declared on
    /// two roles of one family and that is deliberate: <c>GetManyAsync</c> is a batch identity load
    /// <em>and</em> the hydration step of a list, so a caller that only hydrates should not have to
    /// take the write side to do it. One implementation satisfies every role of its family, is
    /// registered once, and each role is forwarded to it.
    /// </para>
    /// </summary>
    private static readonly string[][] Families =
    [
        ["IHousingListingRepository", "IHousingListingSearch", "IHousingListingGuards",
            "IHousingListingStatistics"],
        ["IMarketplaceListingRepository", "IMarketplaceListingSearch",
            "IMarketplaceListingSellerFacts", "IMarketplaceListingStatistics"],
        ["IProviderRepository", "IProviderSearch", "IProviderMembership", "IProviderStatistics"],
        ["ICommunityEventRepository", "ICommunityEventSearch", "ICommunityEventGuards",
            "ICommunityEventSeries", "ICommunityEventStatistics"],
        ["IAnalyticsEventRepository", "IAnalyticsCellSource", "IAnalyticsEventReports",
            "IAnalyticsEventHousekeeping"],
        ["ICreatorRewardRepository", "ICreatorPointRuleRepository",
            "ICreatorRewardRedemptionRepository"],
        ["IJobApplicationRepository", "IJobApplicationSearch", "IJobApplicationFacts"],
        ["IAcademyRepository", "IAcademyCatalogue"],
        ["IAdministrativeAreaRepository", "IAdministrativeAreaSearch",
            "IAdministrativeAreaGeometry"],
        ["ICommunityInitiativeRepository", "ICommunityInitiativeParticipants",
            "ICommunityInitiativeUpdates"],
        ["ICommunityPollRepository", "ICommunityPollResponses", "ICommunityPollResults"],
        ["IProgrammeMembershipRepository", "IProgrammeMembershipRenewals",
            "IProgrammeMembershipReports"],
        ["IGuideRepository", "IGuideReader", "IGuideSearch"],
        ["IJobPostingRepository", "IJobPostingReader", "IJobPostingSearch"],
        ["INotificationRepository", "INotificationChannels", "INotificationInbox"],
        ["IAnalyticsRollupRepository", "IAnalyticsRollupReports",
            "IAnalyticsRollupHousekeeping"],
        ["ICommunityPostRepository", "ICommunityPostReader", "ICommunityPostSearch"],
        ["IConversationRepository", "IConversationInbox", "IConversationNudges"],
        ["IEventRsvpRepository", "IEventRsvpSearch", "IEventRsvpFacts"],
        ["IOrganizationRepository", "IOrganizationSearch", "IOrganizationGuards",
            "IOrganizationStatistics"],
        ["IPlacementRepository", "IPlacementSchedule", "IPlacementFacts"],
    ];

    private const int Ceiling = 12;

    // ── The ceiling, with nothing above it ───────────────────────────────

    /// <summary>
    /// <b>The finding's first criterion, as a standing rule.</b> No repository interface on the
    /// platform declares more than twelve members.
    /// <para>
    /// This replaced a recorded list of the ones that still did. A list that names its exceptions
    /// stops a tail growing; an empty ceiling is the thing the list was a step towards, and it
    /// cannot drift without a failure. A new repository that needs a thirteenth member is a
    /// repository that is answering two kinds of question.
    /// </para>
    /// </summary>
    [Fact]
    public void No_repository_interface_exceeds_the_ceiling()
    {
        var oversized = InterfaceMembers()
            .Where(pair => pair.Key.StartsWith('I')
                && pair.Key.EndsWith("Repository", StringComparison.Ordinal))
            .Where(pair => pair.Value > Ceiling)
            .Select(pair => $"{pair.Key}={pair.Value}")
            .Order(StringComparer.Ordinal)
            .ToList();

        oversized.Should().BeEmpty(
            "a repository that answers more than twelve kinds of question is answering more than " +
            "one kind of question, and its consumers depend on all of them. Offenders: {0}",
            string.Join(", ", oversized));
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
    /// <b>Fields and properties, because a property holder is how the first version of this rule
    /// was evaded without anybody meaning to.</b> Wave three found two abstract base classes
    /// exposing their repository as <c>protected IPlacementRepository Placements { get; }</c> and
    /// <c>protected ICommunityPostRepository Posts { get; }</c>; ten feed providers inherited the
    /// second one to call a single query method. A field scan sees neither. Locals and parameters
    /// stay out: a local resolved from a scope inside a sweep is bound and used within a few lines
    /// where the mismatch is visible, while a field or property is part of the type's shape and
    /// outlives the reason it was added.
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
            foreach (var (type, name) in Holders(file))
            {
                if (!byFamily.TryGetValue(type, out var family))
                {
                    continue;
                }

                holders++;

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
            150, "the split families are injected widely; finding almost none means this rule is " +
                 "reading the wrong thing rather than that the platform is clean");

        offenders.Should().BeEmpty(
            "a role is the set of questions a consumer actually asks. Offenders: {0}",
            string.Join(" | ", offenders));
    }

    // ── Reading the source ───────────────────────────────────────────────

    /// <summary>
    /// The long-lived holders in one file: every field and every property, with its declared type.
    /// </summary>
    private static IEnumerable<(string Type, string Name)> Holders(SourceFile file)
    {
        foreach (var field in file.Root.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            yield return (field.Declaration.Type.ToString(),
                field.Declaration.Variables[0].Identifier.ValueText);
        }

        foreach (var property in file.Root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
        {
            if (property.Parent is InterfaceDeclarationSyntax)
            {
                continue;
            }

            yield return (property.Type.ToString(), property.Identifier.ValueText);
        }
    }

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
