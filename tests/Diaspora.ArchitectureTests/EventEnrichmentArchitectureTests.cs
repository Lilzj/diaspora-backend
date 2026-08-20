using System.Reflection;
using Diaspora.Contracts.Events;
using Diaspora.Modules.Notifications;
using Diaspora.Modules.Recommendations;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetArchTest.Rules;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// B13.1's architecture rules: enrichments are additive, they are owned by the module that
/// publishes them, and no downstream consumer resolves an owner by asking upstream.
/// <para>
/// <b>The rule these exist to protect is a one-line principle with a very easy violation.</b> A
/// published integration event must carry enough context for a consumer to do its job — and the
/// moment it does not, the shortest fix available to whoever is fixing it is a project reference
/// and a repository call. That change is small, obviously correct in review, and permanently
/// welds two modules together. These rules make it a failing build instead.
/// </para>
/// </summary>
public sealed class EventEnrichmentArchitectureTests
{
    /// <summary>The names B13.1 introduced. See the Event Contract Enrichment Guide.</summary>
    private static readonly string[] RecipientProperties =
        ["CandidateUserId", "EmployerUserId", "OrganizerUserId", "AuthorUserId", "OwnerUserId"];

    /// <summary>
    /// The four namespaces this phase touched, and the module directory that owns each.
    /// <para>
    /// <b>Scoped to four rather than swept across the whole contracts assembly, on purpose.</b>
    /// <c>OwnerUserId</c> means a landlord in Housing, a seller in Marketplace and a projected
    /// candidate's owner in Recommendations; a rule keyed on the name alone would report all of
    /// them and be quietly disabled by the second person who hit it. Ownership is a statement
    /// about a contract, not about a word.
    /// </para>
    /// </summary>
    private static readonly (string Namespace, string ModuleDirectory)[] EnrichedModules =
    [
        ("Diaspora.Contracts.Jobs.V1.Events", "src/Diaspora.Modules.Jobs/"),
        ("Diaspora.Contracts.CommunityLife.V1.Events", "src/Diaspora.Modules.CommunityLife/"),
        ("Diaspora.Contracts.Guides.V1.Events", "src/Diaspora.Modules.Guides/"),
        ("Diaspora.Contracts.Providers.V1.Events", "src/Diaspora.Modules.Providers/")
    ];

    private static readonly Assembly Contracts = typeof(IIntegrationEvent).Assembly;

    // ── Additive only ────────────────────────────────────────────────────

    /// <summary>
    /// <b>No recipient identifier anywhere in the published contracts is a value type that cannot
    /// express absence.</b> A non-nullable <c>Guid</c> forces a publisher to invent something when
    /// it does not know the answer, and what it invents is <see cref="Guid.Empty"/> — which is not
    /// "nobody", it is an identifier that will be addressed. Three Jobs contracts shipped that way
    /// before B13.1 and the notifications went nowhere for months.
    /// <para>
    /// The exception list is short and each entry is a genuine invariant: the module raising the
    /// event holds the member on the aggregate and cannot fail to know it.
    /// </para>
    /// </summary>
    [Fact]
    public void A_recipient_identifier_is_nullable_unless_the_publisher_can_never_not_know_it()
    {
        // Contracts whose recipient is required, and can be, because the aggregate that raises
        // the domain event carries the member itself. Named individually rather than pattern
        // matched, so adding a fourth is a decision somebody writes down.
        var alwaysKnown = new[]
        {
            "Diaspora.Contracts.Jobs.V1.Events.ApplicationSubmittedIntegrationEvent.ApplicantUserId",
            "Diaspora.Contracts.Jobs.V1.Events.CandidateShortlistedIntegrationEvent.ApplicantUserId",
            "Diaspora.Contracts.Jobs.V1.Events.InterviewScheduledIntegrationEvent.CandidateUserId",
            "Diaspora.Contracts.Jobs.V1.Events.OfferExtendedIntegrationEvent.CandidateUserId",
            "Diaspora.Contracts.Jobs.V1.Events.EmployerRegisteredIntegrationEvent.OwnerUserId",
            "Diaspora.Contracts.CommunityLife.V1.Events.EventCreatedIntegrationEvent.OrganizerUserId",
            "Diaspora.Contracts.CommunityLife.V1.Events.EventPublishedIntegrationEvent.OrganizerUserId",
            "Diaspora.Contracts.CommunityLife.V1.Events.EventCompletedIntegrationEvent.OrganizerUserId"
        };

        var offenders = EnrichedContracts()
            .SelectMany(contract => contract.GetProperties(), (contract, declared) => (contract, declared))
            .Where(entry => RecipientProperties.Contains(entry.declared.Name, StringComparer.Ordinal))
            .Where(entry => entry.declared.PropertyType == typeof(Guid))
            .Select(entry => $"{entry.contract.FullName}.{entry.declared.Name}")
            .Where(name => !alwaysKnown.Contains(name, StringComparer.Ordinal))
            .ToList();

        offenders.Should().BeEmpty(
            "a recipient the publisher may not know must be nullable, or the publisher will " +
            "write an identifier nobody holds");
    }

    /// <summary>
    /// Every recipient identifier is immutable, like everything else on a published contract. A
    /// settable one would let a consumer rewrite who an event it received was about.
    /// </summary>
    [Fact]
    public void Every_recipient_identifier_is_init_only()
    {
        var offenders = EnrichedContracts()
            .SelectMany(contract => contract.GetProperties(), (contract, declared) => (contract, declared))
            .Where(entry => RecipientProperties.Contains(entry.declared.Name, StringComparer.Ordinal))
            .Where(entry => entry.declared.SetMethod is null
                || !entry.declared.SetMethod.ReturnParameter.GetRequiredCustomModifiers()
                    .Any(modifier => modifier.Name == "IsExternalInit"))
            .Select(entry => $"{entry.contract.FullName}.{entry.declared.Name}")
            .ToList();

        offenders.Should().BeEmpty("a published contract is immutable, recipients included");
    }

    // ── Ownership ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Only the owning module constructs its own published contracts.</b> Which is how "the
    /// publishing module owns the enrichment" is actually enforced: a recipient identity is only
    /// ever written by whoever builds the payload, and nobody outside the module builds one.
    /// <para>
    /// This catches the specific thing B13.1 was warned against — a second module deciding it
    /// knows who owns a job, an event, a guide or a provider, and saying so on the bus. Two
    /// modules publishing the same fact about the same aggregate is how a platform ends up with
    /// two answers to "who should be told" and no way to tell which is current.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("Diaspora.Contracts.Jobs.V1.Events", "src/Diaspora.Modules.Jobs/")]
    [InlineData("Diaspora.Contracts.CommunityLife.V1.Events", "src/Diaspora.Modules.CommunityLife/")]
    [InlineData("Diaspora.Contracts.Guides.V1.Events", "src/Diaspora.Modules.Guides/")]
    [InlineData("Diaspora.Contracts.Providers.V1.Events", "src/Diaspora.Modules.Providers/")]
    public void Only_the_owning_module_constructs_its_published_contracts(
        string contractNamespace, string owningModule)
    {
        var owned = Contracts.GetTypes()
            .Where(type => string.Equals(type.Namespace, contractNamespace, StringComparison.Ordinal))
            .Where(type => type.IsAssignableTo(typeof(IIntegrationEvent)))
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

        owned.Should().NotBeEmpty("{0} declares published contracts", contractNamespace);

        // Matched on the file's imports as well as the type's simple name, because simple names
        // collide across modules on purpose: Jobs and Marketplace both publish an
        // OfferAcceptedIntegrationEvent, Trust and Community Life both an
        // OrganizationVerifiedIntegrationEvent. C# resolves those through the using directives, so
        // a rule that ignored them would report the wrong module for both.
        var offenders = SourceTree.ProductionFiles
            .Where(file => !file.Path.StartsWith(owningModule, StringComparison.Ordinal))
            .Where(file => Imports(file, contractNamespace))
            .SelectMany(
                file => file.Root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>(),
                (file, creation) => (file, creation))
            .Where(entry => owned.Contains(TypeNameOf(entry.creation)))
            .Select(entry => $"{entry.file.Locate(entry.creation)} ({TypeNameOf(entry.creation)})")
            .ToList();

        offenders.Should().BeEmpty(
            "{0} is published by {1} and by nobody else. Offenders: {2}",
            contractNamespace,
            owningModule,
            string.Join(", ", offenders));
    }

    /// <summary>
    /// And the enrichments themselves are assigned only where those contracts are constructed —
    /// which after the rule above means inside the owning module, in its one relay.
    /// </summary>
    [Fact]
    public void Every_recipient_assignment_sits_inside_a_published_contract_initialiser()
    {
        var owned = EnrichedContracts().Select(type => type.Name).ToHashSet(StringComparer.Ordinal);

        var offenders = SourceTree.ProductionFiles
            .Where(file => file.Path.StartsWith("src/Diaspora.Modules.", StringComparison.Ordinal))
            .Where(file => EnrichedModules.Any(module => Imports(file, module.Namespace)))
            .SelectMany(
                file => file.Root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>(),
                (file, creation) => (file, creation))
            .Where(entry => owned.Contains(TypeNameOf(entry.creation)))
            .SelectMany(
                entry => entry.creation.Initializer?.Expressions
                             .OfType<AssignmentExpressionSyntax>()
                         ?? Enumerable.Empty<AssignmentExpressionSyntax>(),
                (entry, assignment) => (entry.file, entry.creation, assignment))
            .Where(entry => entry.assignment.Left is IdentifierNameSyntax name
                && RecipientProperties.Contains(name.Identifier.ValueText, StringComparer.Ordinal))
            .Where(entry => !entry.file.Path.Contains(
                "IntegrationEventRelay", StringComparison.Ordinal))
            .Select(entry => entry.file.Locate(entry.assignment))
            .ToList();

        offenders.Should().BeEmpty(
            "a recipient identity is stamped in the module's one translation seam, so there is " +
            "one place to read to know how it is resolved. Offenders: {0}",
            string.Join(", ", offenders));
    }

    // ── Downstream consumers ─────────────────────────────────────────────

    /// <summary>
    /// <b>Communication still depends on published contracts and nothing else.</b> B13.1's whole
    /// justification was that enriching a contract is cheaper than a module reference; this is the
    /// rule that would have failed if anybody had taken the other route.
    /// </summary>
    [Fact]
    public void Communication_depends_on_no_business_module()
    {
        DependsOnNoBusinessModule(typeof(NotificationsModule).Assembly)
            .Should().BeTrue("the Communication Platform resolves recipients from payloads alone");
    }

    /// <summary>The same claim for the Recommendation Platform, which reads the same enrichments.</summary>
    [Fact]
    public void Recommendation_depends_on_no_business_module()
    {
        DependsOnNoBusinessModule(typeof(RecommendationsModule).Assembly)
            .Should().BeTrue("the Recommendation Platform consumes published contracts alone");
    }

    /// <summary>
    /// <b>No downstream consumer names an upstream aggregate, an identifier or a repository.</b>
    /// The dependency rules above catch a project reference and are exact; this catches the shape
    /// that would come first — a consumer file mentioning a type from the module it consumes,
    /// which is what somebody writes just before they add the reference that would make it
    /// compile.
    /// </summary>
    [Theory]
    [InlineData("src/Diaspora.Modules.Notifications/Application/Consumers/")]
    [InlineData("src/Diaspora.Modules.Recommendations/Application/Consumers/")]
    public void No_downstream_consumer_names_an_upstream_module(string consumerDirectory)
    {
        var offenders = SourceTree.ProductionFiles
            .Where(file => file.Path.StartsWith(consumerDirectory, StringComparison.Ordinal))
            .SelectMany(
                file => file.Root.DescendantNodes().OfType<UsingDirectiveSyntax>(),
                (file, directive) => (file, name: directive.Name?.ToString() ?? string.Empty))
            .Where(entry => EnrichedModules.Any(module => entry.name.StartsWith(
                $"Diaspora.Modules.{ModuleNameOf(module.ModuleDirectory)}.",
                StringComparison.Ordinal)))
            .Select(entry => $"{entry.file.Path}: {entry.name}")
            .ToList();

        offenders.Should().BeEmpty(
            "a consumer that needs an upstream type to find its recipient needs a contract " +
            "enrichment instead. Offenders: {0}",
            string.Join(", ", offenders));
    }

    /// <summary>
    /// <b>A publishing relay resolves recipients inside its own module.</b> The counterpart to the
    /// rule above: the reads B13.1 added are all in relays, and a relay reading another module's
    /// repository would move the coupling rather than remove it.
    /// </summary>
    [Theory]
    [InlineData("src/Diaspora.Modules.Jobs/Application/EventHandlers/JobIntegrationEventRelay.cs", "Jobs")]
    [InlineData("src/Diaspora.Modules.CommunityLife/Application/EventHandlers/CommunityLifeIntegrationEventRelay.cs", "CommunityLife")]
    [InlineData("src/Diaspora.Modules.Guides/Application/EventHandlers/GuideIntegrationEventRelay.cs", "Guides")]
    [InlineData("src/Diaspora.Modules.Providers/Application/EventHandlers/ProviderIntegrationEventRelay.cs", "Providers")]
    public void A_relay_reads_only_its_own_module(string relayPath, string module)
    {
        var relay = SourceTree.ProductionFiles
            .SingleOrDefault(file => string.Equals(file.Path, relayPath, StringComparison.Ordinal));

        relay.Should().NotBeNull("{0} is the module's one translation seam", relayPath);

        var offenders = relay!.Root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Select(directive => directive.Name?.ToString() ?? string.Empty)
            .Where(name => name.StartsWith("Diaspora.Modules.", StringComparison.Ordinal))
            .Where(name => !name.StartsWith(
                $"Diaspora.Modules.{module}.", StringComparison.Ordinal))
            .ToList();

        offenders.Should().BeEmpty(
            "{0}'s relay resolves recipients from its own aggregates. Offenders: {1}",
            module,
            string.Join(", ", offenders));
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>The published contracts of the four modules this phase enriched.</summary>
    private static IEnumerable<Type> EnrichedContracts() =>
        Contracts.GetTypes()
            .Where(type => type.IsAssignableTo(typeof(IIntegrationEvent)) && !type.IsAbstract)
            .Where(type => EnrichedModules.Any(module => string.Equals(
                type.Namespace, module.Namespace, StringComparison.Ordinal)));

    /// <summary>Whether a file imports a namespace, which is how C# resolves a colliding simple name.</summary>
    private static bool Imports(SourceFile file, string contractNamespace) =>
        file.Root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Any(directive => string.Equals(
                directive.Name?.ToString(), contractNamespace, StringComparison.Ordinal));

    /// <summary>The simple name of a constructed type, with any generic or qualification stripped.</summary>
    private static string TypeNameOf(ObjectCreationExpressionSyntax creation) =>
        creation.Type switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            _ => string.Empty
        };

    /// <summary><c>src/Diaspora.Modules.Jobs/</c> to <c>Jobs</c>.</summary>
    private static string ModuleNameOf(string moduleDirectory) =>
        moduleDirectory.Replace("src/Diaspora.Modules.", string.Empty, StringComparison.Ordinal)
            .TrimEnd('/');

    private static bool DependsOnNoBusinessModule(Assembly module) =>
        Types.InAssembly(module)
            .Should()
            .NotHaveDependencyOnAny(
                "Diaspora.Modules.Jobs",
                "Diaspora.Modules.CommunityLife",
                "Diaspora.Modules.Guides",
                "Diaspora.Modules.Providers")
            .GetResult()
            .IsSuccessful;
}
