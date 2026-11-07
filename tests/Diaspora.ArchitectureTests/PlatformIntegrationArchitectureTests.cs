using System.Reflection;
using Diaspora.Contracts.Events;
using Diaspora.Modules.Notifications;
using Diaspora.Modules.Recommendations;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// PSR-1.1's rules: the integrations this phase made, pinned so they cannot quietly come apart, and
/// the two platform-wide participation rules it found unenforced.
/// <para>
/// <b>Every rule here exists because PSR-1.1 found the thing it forbids already happening.</b> That
/// is the standard B1.4 set when seven of its own rules were wrong on their first run — a rule
/// written against a hypothetical is a rule nobody trusts, and a rule that has never failed is a rule
/// nobody has tested.
/// </para>
/// </summary>
public sealed class PlatformIntegrationArchitectureTests
{
    private static readonly Assembly Contracts = typeof(IIntegrationEvent).Assembly;

    // ── Configuration ────────────────────────────────────────────────────

    /// <summary>
    /// <b>Every module validates its own configuration at startup.</b>
    /// <para>
    /// Thirteen modules bound twenty option types with <c>Bind</c> and nothing else, while the host,
    /// Infrastructure, the kernel and the Production Platform all validated theirs. The consequence
    /// was not theoretical: a mistyped value in a module section surfaced wherever the option was
    /// first read, and for several of these that is a background sweep at three in the morning rather
    /// than a request anybody was watching.
    /// </para>
    /// <para>
    /// Read from source rather than from a built container, because the failure this prevents is an
    /// author writing the two-line registration and stopping — which is a fact about the text.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_module_option_is_validated_at_startup()
    {
        var offenders = new List<string>();

        foreach (var file in SourceTree.ProductionFiles
            .Where(file => file.Path.Contains("/Diaspora.Modules.", StringComparison.Ordinal)))
        {
            foreach (var invocation in file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax
                    {
                        Name: GenericNameSyntax { Identifier.ValueText: "AddOptions" }
                    })
                {
                    continue;
                }

                // The whole fluent statement, so ValidateOnStart() further down the chain counts.
                var statement = invocation.FirstAncestorOrSelf<StatementSyntax>();

                if (statement?.ToString().Contains("ValidateOnStart", StringComparison.Ordinal) != true)
                {
                    offenders.Add(file.Locate(invocation));
                }
            }
        }

        offenders.Should().BeEmpty(
            "a module option bound without ValidateOnStart fails wherever it is first read, " +
            "which for a sweep is in production and out of hours");
    }

    // ── The Financial integrations PSR-1.1 made ──────────────────────────

    /// <summary>
    /// <b>Communication consumes the Financial Platform.</b> B13 seeded a <c>billing</c> category —
    /// "Payments and receipts" — and nothing routed to it for two phases while B15 published
    /// seventeen contracts and named six that a communication platform would obviously act on.
    /// <para>
    /// Named individually rather than counted, so removing one is a decision somebody writes down
    /// rather than a number quietly going from nine to eight.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("PaymentFailedIntegrationEvent")]
    [InlineData("RefundIssuedIntegrationEvent")]
    [InlineData("SubscriptionActivatedIntegrationEvent")]
    [InlineData("SubscriptionEndedIntegrationEvent")]
    [InlineData("PlacementActivatedIntegrationEvent")]
    [InlineData("PlacementEndedIntegrationEvent")]
    [InlineData("DonationCompletedIntegrationEvent")]
    [InlineData("CommunityContributionCompletedIntegrationEvent")]
    [InlineData("WalletStatusChangedIntegrationEvent")]
    [InlineData("RefundRequestDecidedIntegrationEvent")]
    public void Communication_consumes_the_financial_contracts_it_was_given(string contract) =>
        ConsumedBy(typeof(NotificationsModuleMetadata).Assembly, contract)
            .Should().BeTrue($"{contract} carries a fact a member is entitled to hear about their money");

    /// <summary>
    /// <b>Recommendation consumes the two placement contracts.</b> B15's event catalogue states the
    /// intent without ambiguity — the placement contracts "carry a subject, a kind and a
    /// <c>PriorityWeight</c>. B14 decides weighting, ranking and personalization; B15 never learns
    /// what it did." Nothing consumed them for two phases.
    /// </summary>
    [Theory]
    [InlineData("PlacementActivatedIntegrationEvent")]
    [InlineData("PlacementEndedIntegrationEvent")]
    public void Recommendation_consumes_the_placement_contracts(string contract) =>
        ConsumedBy(typeof(RecommendationsModuleMetadata).Assembly, contract)
            .Should().BeTrue($"{contract} exists so that ranking can read what was paid for");

    /// <summary>
    /// <b>No Financial consumer writes the editorial featured flag.</b> This is the line B15 drew in
    /// its architecture guide and the one an implementer would most plausibly cross, because setting
    /// <c>IsFeatured = true</c> is a one-line change that appears to work.
    /// <para>
    /// Crossing it would make <em>"did this rank because it is good or because it was paid for"</em>
    /// permanently unanswerable — B15's words — and the symptom would be no symptom at all.
    /// </para>
    /// <para>
    /// <b>Matched on identifiers rather than on text, and the first draft got that wrong.</b> A
    /// textual search failed on <c>PlacementActivatedConsumer</c>, whose documentation says the weight
    /// lands on its own field <em>"never on <c>IsFeatured</c>"</em> — the rule fired on a comment
    /// explaining why the rule holds. Comments are trivia and never appear as syntax nodes, so
    /// walking the tree narrows the rule to code without weakening it: an assignment, a named
    /// argument and a member access are all still caught.
    /// </para>
    /// </summary>
    [Fact]
    public void No_financial_consumer_sets_the_editorial_featured_flag()
    {
        var offenders = new List<string>();

        foreach (var file in SourceTree.ProductionFiles
            .Where(file => file.Path.Contains("/Consumers/Financial/", StringComparison.Ordinal)))
        {
            foreach (var identifier in file.Root.DescendantTokens()
                .Where(token => token.ValueText is "IsFeatured" or "isFeatured"))
            {
                offenders.Add(file.Locate(identifier.Parent!));
            }
        }

        offenders.Should().BeEmpty(
            "a purchase is financial state and the featured flag is display state; " +
            "one field holding both makes paid placement undetectable");
    }

    /// <summary>
    /// <b>Neither consuming module took a reference on the Financial module to do any of this.</b>
    /// The point of the whole arrangement: a consumer depends on published contracts, and the fix for
    /// an insufficient payload is to enrich the contract rather than to reach into the module.
    /// </summary>
    [Theory]
    [InlineData(typeof(NotificationsModuleMetadata))]
    [InlineData(typeof(RecommendationsModuleMetadata))]
    public void No_consuming_module_references_the_financial_module(Type moduleMarker)
    {
        ArgumentNullException.ThrowIfNull(moduleMarker);

        moduleMarker.Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Should().NotContain("Diaspora.Modules.Financial");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Whether the assembly declares a handler closed over the named contract. Matched on the
    /// contract's short name because a consumer's base class is generic over the type itself, so a
    /// rename breaks compilation before it reaches here.
    /// </summary>
    private static bool ConsumedBy(Assembly assembly, string contractName)
    {
        var contract = Contracts.GetTypes()
            .SingleOrDefault(type =>
                type.Name == contractName
                && type.Namespace == "Diaspora.Contracts.Financial.V1.Events");

        contract.Should().NotBeNull($"{contractName} must exist to be consumed");

        return assembly.GetTypes().Any(type =>
            !type.IsAbstract
            && Ancestry(type).Any(ancestor =>
                ancestor.IsGenericType
                && ancestor.GetGenericArguments().Contains(contract)));
    }

    private static IEnumerable<Type> Ancestry(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            yield return current;
        }
    }
}
