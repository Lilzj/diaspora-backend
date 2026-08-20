using System.Reflection;
using Diaspora.Modules.Financial;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Modules.Financial.Domain.Webhooks;
using Diaspora.Modules.Financial.Domain.Withdrawals;
using Diaspora.Modules.Financial.Extensibility.Entitlements;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using FluentAssertions;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The boundaries B15.1 adds, enforced rather than documented.
/// <para>
/// Each of these is a claim the phase makes about money that would otherwise be a sentence in a
/// report nobody re-reads — and every one of them describes something a later author could
/// reasonably want to do, which is exactly why the check has to fail the build rather than a review.
/// </para>
/// </summary>
public class FinancialMovementArchitectureTests
{
    private static readonly Assembly Module = typeof(FinancialModule).Assembly;

    /// <summary>
    /// <b>No type in this module can hold a payout credential.</b> The account number goes to the
    /// settlement provider through its own SDK and never reaches this platform, which is a guarantee
    /// a schema can be audited for rather than a promise somebody has to keep. The check is
    /// deliberately crude — a property-name match — because the failure it guards against is somebody
    /// adding <c>AccountNumber</c> to a destination for one integration.
    /// </summary>
    [Fact]
    public void No_financial_type_declares_a_property_that_could_hold_a_payout_credential()
    {
        string[] forbidden =
        [
            "AccountNumber", "Iban", "SortCode", "RoutingNumber", "CardNumber", "PrimaryAccountNumber",
            "Cvv", "Cvc", "SecurityCode", "BankAccountNumber", "SwiftCode", "BicCode"
        ];

        var offenders = Module.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => forbidden.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                .Select(property => $"{type.Name}.{property.Name}"))
            .ToList();

        offenders.Should().BeEmpty(
            "a platform that never stores a credential cannot leak one. Offenders: "
            + string.Join(", ", offenders));
    }

    /// <summary>
    /// <b>Settlement stays a seam with nothing behind it in this assembly.</b> B15.1 gave payouts a
    /// contract, a hold, a sweep and a state machine, and deliberately did not give them an engine —
    /// a "simulated settlement provider" shipped for testing convenience would quietly repeal the
    /// claim <c>GET /financial/capabilities</c> makes about this deployment. The suite registers a
    /// fake from the test assembly, which is what a real deployment does with a real one.
    /// </summary>
    [Fact]
    public void Settlement_remains_declared_and_unimplemented_in_this_assembly()
    {
        Module.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(typeof(ISettlementProvider).IsAssignableFrom)
            .Should().BeEmpty(
                "no settlement engine ships here; a deployment registers one and payoutsEnabled "
                + "starts reporting true");
    }

    /// <summary>
    /// <b>Points are not money, expressed as a type rule.</b> A reward account with a currency, an
    /// exponent or a <c>Money</c> on it is one line away from being summed with a wallet balance, and
    /// the platform would be carrying a liability it never decided to take on.
    /// </summary>
    [Fact]
    public void A_reward_account_carries_no_currency_and_no_money()
    {
        var offenders = new[] { typeof(RewardAccount), typeof(RewardEntry) }
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property =>
                    property.PropertyType == typeof(Modules.Financial.Domain.ValueObjects.Money)
                    || property.Name.Contains("Currency", StringComparison.Ordinal)
                    || property.Name.Contains("MinorUnits", StringComparison.Ordinal))
                .Select(property => $"{type.Name}.{property.Name}"))
            .ToList();

        offenders.Should().BeEmpty(
            "a point is a count; what it is worth is a policy read at conversion time. Offenders: "
            + string.Join(", ", offenders));
    }

    /// <summary>
    /// <b>The record of a received callback is append-only, like the ledger.</b> "We received
    /// something we could not verify" and "we received nothing" are the two most different answers to
    /// why a member was not charged, and a row somebody can edit afterwards cannot tell them apart.
    /// </summary>
    [Fact]
    public void A_received_webhook_cannot_be_changed_after_it_is_recorded()
    {
        typeof(ProcessedWebhookEvent)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod is { IsPublic: true })
            .Should().BeEmpty("the record of what arrived is a fact, not a state");

        typeof(ProcessedWebhookEvent)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Should().BeEmpty("there is no operation on a received callback but reading it");
    }

    /// <summary>
    /// <b>A payment's purpose is set once and never afterwards.</b> A purpose a second request could
    /// change is a payment whose counterparty a second request could redirect — which is the whole of
    /// the deposit surface's security.
    /// </summary>
    [Fact]
    public void A_payment_intents_purpose_has_no_public_setter_and_no_mutator()
    {
        typeof(PaymentIntent)
            .GetProperty(nameof(PaymentIntent.Purpose))!
            .SetMethod!.IsPublic.Should().BeFalse();

        typeof(PaymentIntent)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)

            // Property accessors are methods too, and `get_Purpose` is one. The rule is about a
            // mutator somebody wrote, not about the getter the compiler emitted.
            .Where(method => !method.IsSpecialName)
            .Where(method => method.Name.Contains("Purpose", StringComparison.Ordinal))
            .Should().BeEmpty("nothing changes where captured money lands");
    }

    /// <summary>
    /// <b>The withdrawal aggregate holds no balance and sets none.</b> Money moves because a ledger
    /// entry was appended, and a payout is no exception — the aggregate records a request and the
    /// posting service moves the money.
    /// </summary>
    [Fact]
    public void A_withdrawal_has_no_way_to_set_a_balance()
    {
        string[] forbidden =
        [
            "Credit", "Debit", "SetBalance", "AdjustBalance", "Deduct", "Release", "Hold"
        ];

        var offenders = typeof(Withdrawal)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => forbidden.Contains(method.Name, StringComparer.Ordinal))
            .Select(method => method.Name)
            .ToList();

        offenders.Should().BeEmpty(
            "the hold and its release are ledger postings, not properties. Offenders: "
            + string.Join(", ", offenders));
    }

    /// <summary>
    /// <b>The entitlement usage seam is a read and nothing else.</b> A vertical reports how many; the
    /// vertical that owns the limit enforces it, at the moment of the action, against its own data. A
    /// gate that asked B15 would be a gate that failed open whenever B15 was slow.
    /// </summary>
    [Fact]
    public void The_entitlement_usage_seam_only_reports_and_never_decides()
    {
        var methods = typeof(IEntitlementUsageProvider)
            .GetMethods()
            .Where(method => !method.IsSpecialName)
            .ToList();

        methods.Should().ContainSingle("one question: how many");

        methods[0].ReturnType.Should().NotBe<Task<bool>>(
            "a provider reports a count, never a permission");
    }

    /// <summary>
    /// <b>Every published financial contract still lives in the contracts assembly.</b> B15.1 adds
    /// eight, and a consumer must be able to reach one without referencing this module.
    /// </summary>
    [Fact]
    public void The_b15_1_contracts_are_published_from_the_contracts_assembly()
    {
        var published = typeof(Diaspora.Contracts.Events.IIntegrationEvent).Assembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith(
                "Diaspora.Contracts.Financial", StringComparison.Ordinal) == true)
            .Where(type => typeof(Diaspora.Contracts.Events.IIntegrationEvent).IsAssignableFrom(type))
            .Select(type => type.Name)
            .ToList();

        published.Should().Contain(
        [
            "DepositCompletedIntegrationEvent",
            "WithdrawalStatusChangedIntegrationEvent",
            "RefundRequestedIntegrationEvent",
            "RefundRequestDecidedIntegrationEvent",
            "RewardPointsGrantedIntegrationEvent",
            "RewardPointsConvertedIntegrationEvent",
            "SubscriptionExpiringIntegrationEvent",
            "PlacementExpiringIntegrationEvent"
        ]);
    }

    /// <summary>
    /// <b>Money never reaches the outbox as a decimal.</b> Every published financial contract carries
    /// minor units and a currency string; a decimal on the bus is a decimal through a JSON parser, a
    /// JavaScript consumer and an analytics pipeline, and it does not survive all three.
    /// </summary>
    [Fact]
    public void No_published_financial_contract_carries_a_decimal_amount()
    {
        var offenders = typeof(Diaspora.Contracts.Events.IIntegrationEvent).Assembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith(
                "Diaspora.Contracts.Financial", StringComparison.Ordinal) == true)
            .SelectMany(type => type.GetProperties()
                .Where(property => property.PropertyType == typeof(decimal))
                .Where(property => !property.Name.Contains("Rate", StringComparison.Ordinal))
                .Where(property => !property.Name.Contains("Weight", StringComparison.Ordinal))
                .Select(property => $"{type.Name}.{property.Name}"))
            .ToList();

        offenders.Should().BeEmpty(
            "an amount on the bus is minor units and a currency. A rate is not an amount. "
            + "Offenders: " + string.Join(", ", offenders));
    }
}
