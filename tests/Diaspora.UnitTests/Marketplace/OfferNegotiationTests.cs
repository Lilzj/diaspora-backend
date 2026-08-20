using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.UnitTests.Marketplace.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Marketplace;

/// <summary>
/// Negotiation: the turn rule, the history and the round ceiling.
/// </summary>
public class OfferNegotiationTests
{
    private static readonly DateTimeOffset Now = MarketplaceFixtures.Now;

    private static OfferTerms Terms(decimal amount, int quantity = 1) =>
        OfferTerms.Create(amount, "EUR", quantity, null).Value;

    [Fact]
    public void A_new_negotiation_waits_on_the_seller_and_records_its_first_round()
    {
        var offer = MarketplaceFixtures.Offer(amount: 200m);

        offer.Status.Should().Be(OfferStatus.Pending);
        offer.AwaitingParty.Should().Be(OfferParty.Seller);
        offer.Rounds.Should().Be(1);
        offer.History[0].Action.Should().Be(NegotiationAction.Offered);
        offer.History[0].Amount.Should().Be(200m);
    }

    /// <summary>
    /// <b>The turn rule is the invariant that makes this a negotiation.</b> A buyer cannot accept
    /// their own offer, and a seller cannot counter their own counter.
    /// </summary>
    [Fact]
    public void A_buyer_cannot_accept_their_own_offer()
    {
        var buyer = MarketplaceFixtures.NewUserId();
        var offer = MarketplaceFixtures.Offer(buyerId: buyer);

        var result = offer.Accept(buyer, Now, Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("marketplace.offer.not-yours");
    }

    [Fact]
    public void A_counter_hands_the_turn_back_and_appends_to_the_history()
    {
        var buyer = MarketplaceFixtures.NewUserId();
        var seller = MarketplaceFixtures.NewUserId();
        var offer = MarketplaceFixtures.Offer(
            buyerId: buyer, sellerUserId: seller, amount: 200m);

        offer.Counter(seller, Terms(230m), Now.AddDays(2), Now, Guid.NewGuid())
            .IsSuccess.Should().BeTrue();

        offer.Status.Should().Be(OfferStatus.Countered);
        offer.AwaitingParty.Should().Be(OfferParty.Buyer);
        offer.Terms.Amount.Should().Be(230m);
        offer.Rounds.Should().Be(2);

        offer.Counter(buyer, Terms(215m), Now.AddDays(2), Now, Guid.NewGuid())
            .IsSuccess.Should().BeTrue();

        offer.AwaitingParty.Should().Be(OfferParty.Seller);
        offer.Rounds.Should().Be(3);
    }

    /// <summary>
    /// <b>This is the brief's "complete negotiation history".</b> The value of the aggregate is
    /// that a disputed sale can be read back move by move.
    /// </summary>
    [Fact]
    public void Every_round_survives_to_the_settlement()
    {
        var buyer = MarketplaceFixtures.NewUserId();
        var seller = MarketplaceFixtures.NewUserId();
        var offer = MarketplaceFixtures.Offer(
            buyerId: buyer, sellerUserId: seller, amount: 200m);

        offer.Counter(seller, Terms(240m), null, Now, Guid.NewGuid());
        offer.Counter(buyer, Terms(220m), null, Now, Guid.NewGuid());
        offer.Accept(seller, Now, Guid.NewGuid());

        offer.Status.Should().Be(OfferStatus.Accepted);
        offer.History.Should().HaveCount(4);

        offer.History.Select(entry => entry.Action).Should().ContainInOrder(
            NegotiationAction.Offered,
            NegotiationAction.Countered,
            NegotiationAction.Countered,
            NegotiationAction.Accepted);

        offer.History.Select(entry => entry.Party).Should().ContainInOrder(
            OfferParty.Buyer, OfferParty.Seller, OfferParty.Buyer, OfferParty.Seller);
    }

    /// <summary>
    /// The ceiling stops a scripted client running a negotiation forever against a seller who
    /// cannot leave it.
    /// </summary>
    [Fact]
    public void The_round_ceiling_ends_the_haggling()
    {
        var buyer = MarketplaceFixtures.NewUserId();
        var seller = MarketplaceFixtures.NewUserId();
        var offer = MarketplaceFixtures.Offer(buyerId: buyer, sellerUserId: seller);

        var party = seller;

        while (offer.Rounds < MarketplaceLimits.MaxNegotiationRounds)
        {
            offer.Counter(party, Terms(200m + offer.Rounds), null, Now, Guid.NewGuid())
                .IsSuccess.Should().BeTrue();

            party = party == seller ? buyer : seller;
        }

        var refused = offer.Counter(party, Terms(999m), null, Now, Guid.NewGuid());

        refused.IsFailure.Should().BeTrue();
        refused.Error.Code.Should().Be("marketplace.offer.rounds.limit");

        // Settling is still available, which is the point: the parties are told to decide rather
        // than trapped.
        offer.Reject(party, "No deal.", Now, Guid.NewGuid()).IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// <b>Withdrawing is the mirror image of the turn rule.</b> It is the party who is <em>not</em>
    /// being waited on who may take their move back, and it must never be blocked.
    /// </summary>
    [Fact]
    public void Either_party_may_withdraw_whenever_the_negotiation_is_open()
    {
        var buyer = MarketplaceFixtures.NewUserId();
        var offer = MarketplaceFixtures.Offer(buyerId: buyer);

        offer.AwaitingParty.Should().Be(OfferParty.Seller, "it is not the buyer's turn");

        offer.Withdraw(buyer, "Found one elsewhere.", Now, Guid.NewGuid())
            .IsSuccess.Should().BeTrue();

        offer.Status.Should().Be(OfferStatus.Withdrawn);
    }

    [Fact]
    public void A_settled_negotiation_cannot_be_reopened()
    {
        var seller = MarketplaceFixtures.NewUserId();
        var offer = MarketplaceFixtures.Offer(sellerUserId: seller);

        offer.Reject(seller, null, Now, Guid.NewGuid()).IsSuccess.Should().BeTrue();

        var reopened = offer.Counter(seller, Terms(300m), null, Now, Guid.NewGuid());

        reopened.IsFailure.Should().BeTrue();
        reopened.Error.Code.Should().Be("marketplace.offer.already-decided");
    }

    /// <summary>
    /// The sweep's entry point, and it is quiet: an offer that has already settled or whose clock
    /// has not run out succeeds and does nothing, so a retried batch does not thrash.
    /// </summary>
    [Fact]
    public void Expiry_is_idempotent_and_only_fires_once_the_clock_has_passed()
    {
        var offer = MarketplaceFixtures.Offer(expiresAt: Now.AddHours(1));

        offer.Expire(Now, Guid.NewGuid()).IsSuccess.Should().BeTrue();
        offer.Status.Should().Be(OfferStatus.Pending, "the clock has not run out");

        offer.Expire(Now.AddHours(2), Guid.NewGuid()).IsSuccess.Should().BeTrue();
        offer.Status.Should().Be(OfferStatus.Expired);

        offer.Expire(Now.AddHours(3), Guid.NewGuid()).IsSuccess.Should().BeTrue();
        offer.History.Count(entry => entry.Action == NegotiationAction.Expired)
            .Should().Be(1, "a second pass must not append a second expiry");
    }

    [Fact]
    public void An_offer_in_another_currency_is_refused_rather_than_compared()
    {
        var seller = MarketplaceFixtures.NewUserId();
        var offer = MarketplaceFixtures.Offer(sellerUserId: seller);

        var wrongCurrency = OfferTerms.Create(200m, "GBP", 1, null).Value;

        var result = offer.Counter(seller, wrongCurrency, null, Now, Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("marketplace.currency.mismatch");
    }

    /// <summary>
    /// The seller's private floor is enforced and never disclosed — a visible floor is just a lower
    /// asking price.
    /// </summary>
    [Fact]
    public void A_private_minimum_refuses_a_low_offer_without_naming_the_number()
    {
        var price = ListingPrice.Negotiable(250m, "EUR", minimumOffer: 200m).Value;

        price.Accepts(180m).Should().BeFalse();
        price.Accepts(200m).Should().BeTrue();
        price.Accepts(260m).Should().BeTrue();
    }

    /// <summary>
    /// A floor above the asking price is a seller who confused the two fields, and accepting it
    /// would refuse every offer including one at the full asking price.
    /// </summary>
    [Fact]
    public void A_minimum_above_the_asking_price_is_refused()
    {
        ListingPrice.Negotiable(100m, "EUR", minimumOffer: 150m).IsFailure.Should().BeTrue();
    }
}
