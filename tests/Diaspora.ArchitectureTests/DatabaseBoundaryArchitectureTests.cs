using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// BARCH-004. One <c>DbContext</c> for 21 modules, and the boundary made explicit rather than split.
/// <para>
/// The finding records three consequences, two deliberate and one not. The deliberate two are the
/// trade the modular monolith is built on: a transaction spans modules, which is what makes
/// <c>[Transactional]</c> work across the CommunityProgramme → Financial settlement, and there is
/// one model cache, one migration history and one connection pool. <b>The third is that any module
/// holding the context can query another module's schema, and nothing stopped it.</b>
/// </para>
/// <para>
/// <b>Two of the finding's premises have expired, and one hole is sharper than it says.</b> It
/// argues the type references exist because BARCH-001 counted 107 internal imports; BARCH-001
/// closed on 21 November 2026 with its exemption list empty, so no module can name another
/// module's types at all. Raw SQL, the other way across, appears in four files and all four are in
/// <c>Diaspora.Persistence</c>. What remains is the route neither rule can see: <b>seven identity
/// aggregates — <c>User</c>, <c>UserSession</c>, <c>UserDevice</c>, <c>OtpChallenge</c>,
/// <c>SecurityEvent</c>, <c>StepUpGrant</c>, <c>AccountActionProposal</c> — live in
/// <c>Diaspora.Domain</c> rather than in a module</b>, so every module may import them, and a
/// module that did <c>Set&lt;UserSession&gt;()</c> would be reading the identity schema without
/// crossing any module boundary a rule was watching.
/// </para>
/// <para>
/// <see cref="A_module_queries_only_entities_it_owns"/> is the rule that closes it. The write side
/// was already enforced at startup — <c>ApplyModuleConfigurations</c> refuses a contributor that
/// maps into another module's schema — and now has a test that watches it refuse
/// (<c>ModuleSchemaOwnershipTests</c>), which it did not before.
/// </para>
/// <para>
/// <b>The context is not split, deliberately.</b> Splitting would cost the cross-module
/// transaction, which is load-bearing for the financial settlement paths. The decision and its
/// three consequences are recorded in <c>docs/adrs/adr-022-one-database-context.md</c>.
/// </para>
/// <para>
/// <b>That ADR was the twenty-third file nobody would have been able to read.</b> When it was
/// written, <c>docs/</c> was gitignored and not one of the platform's 22 decision records was
/// tracked. The register is tracked now, narrowly — the audit reports and phase write-ups are
/// still local — which is why <see cref="The_decision_register_is_in_the_repository"/> exists: the
/// same rule the <c>.gitignore</c> states three times over, that a guardrail's input must be in
/// the repository or the guardrail is a comment. The enforceable half of the decision still lives
/// here and in the context's own summary rather than in the ADR, which is the right division of
/// labour; what tracking buys is that the narrative travels with the code it explains.
/// </para>
/// </summary>
public sealed class DatabaseBoundaryArchitectureTests
{
    private const string Persistence = "Diaspora.Persistence";
    private const string Kernel = "Diaspora.Domain";
    private const string CompositionRoot = "Diaspora.Api";

    /// <summary>
    /// Every file outside <c>Diaspora.Persistence</c> that holds the context without being one of
    /// its module's own repository implementations.
    /// <para>
    /// <b>Exact in both directions.</b> The finding's remediation expected one exemption — the
    /// privacy provider. There are sixteen, in five recognisable shapes: the host's two health
    /// checks and two audience sources, three privacy providers answering an erasure request, four
    /// area filing sources answering B4, two extensibility directories, two analytics sources and
    /// one defaults seeder. Each is legitimate and each is a place the boundary is held by
    /// convention rather than by structure, which is the reason to count them.
    /// </para>
    /// </summary>
    private static readonly string[] ContextHoldersOutsideARepository =
    [
        "src/Diaspora.Api/Health/PlatformHealthChecks.cs",
        "src/Diaspora.Api/Health/TransactionHealthCheck.cs",
        "src/Diaspora.Api/Identity/Audiences/MemberInventorySource.cs",
        "src/Diaspora.Api/Identity/Audiences/ResidenceCountryAudienceResolver.cs",
        "src/Diaspora.Modules.Analytics/Infrastructure/Privacy/AnalyticsPrivacyProviders.cs",
        "src/Diaspora.Modules.Automation/Extensibility/Location/AutomationAreaWatchDirectory.cs",
        "src/Diaspora.Modules.Bookmarks/Extensibility/BookmarkSaverDirectory.cs",
        "src/Diaspora.Modules.CommunityProgramme/Infrastructure/Analytics/CommunityAnalyticsSource.cs",
        "src/Diaspora.Modules.CommunityProgramme/Infrastructure/Privacy/CommunityProgrammePrivacyProviders.cs",
        "src/Diaspora.Modules.Conversations/Infrastructure/ConversationDefaults.cs",
        "src/Diaspora.Modules.Financial/Application/Campaigns/CampaignAnalyticsSource.cs",
        "src/Diaspora.Modules.Housing/Infrastructure/Location/HousingAreaFilingSource.cs",
        "src/Diaspora.Modules.Jobs/Infrastructure/Location/JobAreaFilingSource.cs",
        "src/Diaspora.Modules.Location/Infrastructure/Privacy/LocationPrivacyProviders.cs",
        "src/Diaspora.Modules.Marketplace/Infrastructure/Location/MarketplaceAreaFilingSource.cs",
        "src/Diaspora.Modules.Providers/Infrastructure/Location/ProviderAreaFilingSource.cs",
    ];

    // ── The read side, which nothing watched ─────────────────────────────

    /// <summary>
    /// <b>The finding's "not deliberate" consequence, as a standing rule.</b> Every
    /// <c>Set&lt;T&gt;</c> outside <c>Diaspora.Persistence</c> names an entity the calling project
    /// declares.
    /// <para>
    /// The composition root is the one exception and only for the kernel's own aggregates: it
    /// counts members for the platform overview, and <c>User</c> lives in <c>Diaspora.Domain</c>
    /// because identity is not a module. A module reaching the same way would be reading the
    /// identity schema, and that is exactly what this rule is for.
    /// </para>
    /// </summary>
    [Fact]
    public void A_module_queries_only_entities_it_owns()
    {
        var owner = EntityOwners();
        var offenders = new List<string>();
        var resolved = 0;

        foreach (var file in SourceTree.ProductionFiles)
        {
            var project = ProjectOf(file.Path);

            if (project == Persistence)
            {
                continue;
            }

            foreach (var entity in QueriedEntities(file))
            {
                if (!owner.TryGetValue(entity, out var declaredIn))
                {
                    continue;   // a type parameter, or a type this platform does not declare
                }

                resolved++;

                // A simple name can be declared twice: Trust and Community both declare a
                // ModerationDecision, Trust and Financial both a CommunityContribution. When the
                // calling project declares one of them, that is what C# binds to here — reaching
                // the other would need an import of another module's namespace, which BARCH-001
                // already forbids and whose exemption list is empty.
                if (declaredIn.Contains(project))
                {
                    continue;
                }

                if (project == CompositionRoot && declaredIn.Contains(Kernel))
                {
                    continue;
                }

                offenders.Add(
                    $"{file.Path}: Set<{entity}> reaches " +
                    string.Join(" or ", declaredIn.Order(StringComparer.Ordinal)));
            }
        }

        resolved.Should().BeGreaterThan(
            100, "the modules query their own entities through the context constantly; finding " +
                 "few resolved types means this rule is reading the wrong thing rather than that " +
                 "the platform is clean");

        offenders.Should().BeEmpty(
            "one context means a module can read another module's schema by naming its entity, " +
            "and the seven identity aggregates in Diaspora.Domain are reachable from every module " +
            "without crossing a module boundary at all. Offenders: {0}",
            string.Join(" | ", offenders));
    }

    /// <summary>
    /// The holders outside a repository are exactly the ones recorded.
    /// </summary>
    [Fact]
    public void Only_the_recorded_holders_reach_the_context_outside_a_repository()
    {
        var found = SourceTree.ProductionFiles
            .Where(file => ProjectOf(file.Path) != Persistence)
            .Where(file => !file.Path.Contains("/Persistence/", StringComparison.Ordinal))
            .Where(NamesTheContext)
            .Select(file => file.Path)
            .Order(StringComparer.Ordinal)
            .ToList();

        found.Should().Equal(
            ContextHoldersOutsideARepository,
            "a seventeenth holder is a new place the boundary is kept by convention, and it " +
            "should be a deliberate edit rather than a quiet one. Found: {0}",
            string.Join(", ", found));
    }

    /// <summary>
    /// The decision register is in the repository, and this ADR is in it.
    /// <para>
    /// <b>The <c>.gitignore</c> states the rule three times and this is the fourth.</b> Its own
    /// notes record a test that had never run because its input lived in <c>docs/</c>, and an
    /// alert catalogue that would not have existed on a clean checkout. A register of twenty-three
    /// decision records is the same failure one level up: it explains why the platform is shaped
    /// as it is, and until 21 November 2026 a clean checkout had none of it.
    /// </para>
    /// <para>
    /// This rule is what stops the carve-out being reverted by a tidy-up. It reads the working
    /// tree, so it is only meaningful because these files are now committed — which is the whole
    /// point of it.
    /// </para>
    /// </summary>
    [Fact]
    public void The_decision_register_is_in_the_repository()
    {
        var register = new DirectoryInfo(
            Path.Combine(SourceTree.Root.FullName, "docs", "adrs"));

        register.Exists.Should().BeTrue(
            "the architectural decision register is tracked, and a checkout without it cannot " +
            "tell anybody why this platform is shaped as it is");

        var records = register.GetFiles("adr-*.md");

        records.Should().HaveCountGreaterThanOrEqualTo(
            22, "twenty-two decision records existed when the register was first tracked, and a " +
                "decision is retired by superseding it rather than by deleting the record");

        records.Select(file => file.Name)
            .Should().Contain(
                name => name.StartsWith("adr-022", StringComparison.Ordinal),
                "ADR-022 records the one-context decision these rules enforce");
    }

    /// <summary>
    /// The sanity check every source-reading rule needs. A scan that found no context holders at
    /// all would satisfy the rule above only because its own population was empty.
    /// </summary>
    [Fact]
    public void The_scan_read_the_platforms_context_holders()
    {
        var holders = SourceTree.ProductionFiles.Count(NamesTheContext);

        holders.Should().BeGreaterThan(
            150, "every module's repositories hold the context and there are many; finding few " +
                 "means the scan is reading the wrong thing");
    }

    // ── Reading the source ───────────────────────────────────────────────

    /// <summary>
    /// Whether a file names the context in code. A mention in a doc comment is not a dependency,
    /// and twenty-five files mention it only there.
    /// </summary>
    private static bool NamesTheContext(SourceFile file) =>
        file.Root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Any(identifier => identifier.Identifier.ValueText == "DiasporaDbContext");

    /// <summary>The entity named by each <c>Set&lt;T&gt;</c> call in a file.</summary>
    private static IEnumerable<string> QueriedEntities(SourceFile file) =>
        file.Root.DescendantNodes()
            .OfType<GenericNameSyntax>()
            .Where(generic => generic.Identifier.ValueText == "Set")
            .Where(generic => generic.TypeArgumentList.Arguments.Count == 1)
            .Select(generic => generic.TypeArgumentList.Arguments[0].ToString().Split('.')[^1]);

    /// <summary>
    /// Every class this platform declares, and the projects that declare it.
    /// <para>
    /// A set rather than one project, because a simple name is not unique across 24 projects and
    /// taking the first declaration found reports the other module as an offender — which it did,
    /// three times, for two names.
    /// </para>
    /// </summary>
    private static Dictionary<string, HashSet<string>> EntityOwners()
    {
        var owners = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var file in SourceTree.ProductionFiles)
        {
            var project = ProjectOf(file.Path);

            foreach (var declaration in file.Root.DescendantNodes()
                         .OfType<ClassDeclarationSyntax>())
            {
                if (!owners.TryGetValue(declaration.Identifier.ValueText, out var projects))
                {
                    projects = new HashSet<string>(StringComparer.Ordinal);
                    owners[declaration.Identifier.ValueText] = projects;
                }

                projects.Add(project);
            }
        }

        return owners;
    }

    private static string ProjectOf(string path) => path.Split('/')[1];
}
