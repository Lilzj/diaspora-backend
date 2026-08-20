using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// The fields the frontend removed from a card rather than fill with a plausible value.
/// <para>
/// <b>Asserted over the mapper rather than over an endpoint</b>, because a bare deployment seeds
/// funds and not causes — an integration test would pass vacuously against an empty list, which is a
/// test that proves nothing and reads as though it proved something.
/// </para>
/// </summary>
public class FinancialContractShapeTests
{
    private static readonly DateTimeOffset Opened =
        new(2026, 8, 8, 9, 30, 0, TimeSpan.Zero);

    /// <summary>
    /// GAP-11. The client's card wanted "posted 2 days ago" and the response carried no timestamp of
    /// any kind, so the line was removed rather than filled with a plausible date.
    /// </summary>
    [Fact]
    public void A_donation_cause_response_carries_the_moment_the_cause_was_opened()
    {
        var cause = DonationCause.Open(
            DonationCauseId.New(),
            "flood-relief-2026",
            "Flood relief",
            "Rebuilding after the August floods.",
            BeneficiaryType.Community,
            "Diaspora",
            "GBP",
            Opened);

        var response = FinancialMapper.ToResponse(cause);

        response.CreatedAt.Should().Be(Opened);
    }

    /// <summary>
    /// A client rendering a payment has to know whether the member is paying the platform or paying
    /// themselves. Inferring it from a description would be inferring it from a string somebody will
    /// translate.
    /// </summary>
    [Theory]
    [InlineData(PaymentPurpose.Purchase, nameof(PaymentPurpose.Purchase))]
    [InlineData(PaymentPurpose.Deposit, nameof(PaymentPurpose.Deposit))]
    public void A_payment_response_says_where_the_money_lands(
        PaymentPurpose purpose, string expected)
    {
        var intent = PaymentIntent.Create(
            PaymentIntentId.New(),
            Guid.NewGuid(),
            FinancialAccountId.New(),
            Money.FromMinorUnits(2_500, "GBP"),
            PaymentMethodKind.Card,
            "A test payment",
            Opened,
            purpose: purpose);

        FinancialMapper.ToResponse(intent).Purpose.Should().Be(expected);
    }

    /// <summary>
    /// MON-GAP-004 and MON-GAP-005. A promotion detail screen needs both to say anything true about
    /// what happens next, and the frontend had neither.
    /// </summary>
    [Fact]
    public void A_placement_response_carries_its_auto_renew_preference_and_its_pause()
    {
        var placement = Placement.Purchase(
            PlacementId.New(),
            SubjectReference.Create(MonetizationSubjectType.MarketplaceListing, Guid.NewGuid()),
            PlacementKind.Featured,
            DateWindow.ForDays(Opened, 30),
            2.0m,
            Guid.NewGuid(),
            "featured-listing-30",
            Opened);

        placement.Activate(Opened);
        placement.SetAutoRenew(enabled: true, Opened);
        placement.Pause(Opened.AddDays(1));

        var response = FinancialMapper.ToResponse(placement, Opened.AddDays(2));

        response.AutoRenewEnabled.Should().BeTrue();
        response.PausedAt.Should().Be(Opened.AddDays(1));
        response.Status.Should().Be(nameof(PlacementStatus.Paused));
        response.IsActive.Should().BeFalse("a paused placement is not in force");
    }
}
