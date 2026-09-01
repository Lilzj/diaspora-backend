using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Modules.Media.Extensibility.Access;
using Diaspora.SharedKernel.Identity;
using Diaspora.UnitTests.Marketplace.TestData;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Marketplace;

/// <summary>
/// Whether a stranger may look at a listing's photographs.
/// </summary>
/// <remarks>
/// <para>
/// The answer used to be no, for everyone, always — UAT-121. A media asset is created private and
/// Media had nothing that could tell it a published listing's gallery is not. Every buyer saw a
/// "no photograph" panel on listings that <b>cannot be published without a photograph</b>.
/// </para>
/// <para>
/// The rule under test is one line: a photograph is readable exactly when the listing carrying it
/// is. What the tests below are really pinning is that this policy does not <em>restate</em> that
/// rule — it delegates to the same <c>CanSeeAsync</c> the detail screen uses, so the two cannot
/// drift apart. A drift here would be a leak, not a bug.
/// </para>
/// </remarks>
public sealed class MarketplaceListingMediaPolicyTests
{
    private static readonly UserId Seller = UserId.New();
    private static readonly UserId Stranger = UserId.New();

    [Fact]
    public async Task A_published_listings_gallery_is_readable_by_a_stranger()
    {
        var listing = MarketplaceFixtures.ActiveListing();

        var decision = await Policy(listing, caller: Stranger)
            .EvaluateAsync(Request(listing));

        decision.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task A_draft_listings_gallery_is_not()
    {
        // A draft is nobody's business but the seller's, and its photographs go with it. The
        // seller still sees their own, because Media already grants the uploader — which is why
        // this abstains rather than denying.
        var listing = MarketplaceFixtures.PublishableListing();

        var decision = await Policy(listing, caller: Stranger)
            .EvaluateAsync(Request(listing));

        decision.IsAbstention.Should().BeTrue();
    }

    [Fact]
    public async Task A_document_attached_to_a_published_listing_stays_private()
    {
        // The distinction the role list exists for. Sellers attach proof of ownership, service
        // histories and import receipts to listings; publishing the advertisement does not
        // publish the paperwork.
        var listing = MarketplaceFixtures.ActiveListing();

        var decision = await Policy(listing, caller: Stranger)
            .EvaluateAsync(Request(listing) with { Role = MediaReferenceRoles.Document });

        decision.IsAbstention.Should().BeTrue();
    }

    [Fact]
    public async Task Seeing_a_listing_does_not_confer_the_right_to_change_its_pictures()
    {
        var listing = MarketplaceFixtures.ActiveListing();

        var decision = await Policy(listing, caller: Stranger)
            .EvaluateAsync(Request(listing) with { RequestedLevel = MediaAccessLevel.Manage });

        decision.IsAbstention.Should().BeTrue();
    }

    [Fact]
    public async Task An_asset_pointing_at_a_listing_that_no_longer_exists_abstains()
    {
        // Abstain rather than deny: a denial is final and would take the file away from the
        // member who uploaded it, which is exactly who needs it if the listing is being restored.
        var listings = Substitute.For<IMarketplaceListingRepository>();
        listings.GetForReadAsync(Arg.Any<MarketplaceListingId>(), Arg.Any<CancellationToken>())
            .Returns((MarketplaceListing?)null);

        var decision = await new MarketplaceListingMediaPolicy(listings, Authorization(Stranger))
            .EvaluateAsync(Request(MarketplaceFixtures.ActiveListing()));

        decision.IsAbstention.Should().BeTrue();
    }

    [Fact]
    public async Task It_never_denies()
    {
        // The property that makes this change safe to ship: it can only widen. Whatever the
        // listing's state, nobody loses access they had before this policy existed.
        var listings = Substitute.For<IMarketplaceListingRepository>();
        listings.GetForReadAsync(Arg.Any<MarketplaceListingId>(), Arg.Any<CancellationToken>())
            .Returns((MarketplaceListing?)null);

        var policy = new MarketplaceListingMediaPolicy(listings, Authorization(Stranger));
        var listing = MarketplaceFixtures.ActiveListing();

        MediaAccessRequest[] cases =
        [
            Request(listing),
            Request(listing) with { Role = MediaReferenceRoles.Document },
            Request(listing) with { RequestedLevel = MediaAccessLevel.Manage },
            Request(listing) with { Caller = null }
        ];

        foreach (var probe in cases)
        {
            (await policy.EvaluateAsync(probe)).IsDenial.Should().BeFalse();
        }
    }

    [Fact]
    public void It_speaks_only_for_marketplace_listings()
    {
        Policy(MarketplaceFixtures.ActiveListing(), Stranger)
            .OwnerType.Should().Be(MediaOwnerType.MarketplaceListing);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static MarketplaceListingMediaPolicy Policy(MarketplaceListing listing, UserId caller)
    {
        var listings = Substitute.For<IMarketplaceListingRepository>();
        listings.GetForReadAsync(Arg.Any<MarketplaceListingId>(), Arg.Any<CancellationToken>())
            .Returns(listing);

        return new MarketplaceListingMediaPolicy(listings, Authorization(caller));
    }

    private static MarketplaceAuthorizationService Authorization(UserId caller)
    {
        var currentUser = Substitute.For<Diaspora.Platform.Services.Abstractions.ICurrentUser>();
        currentUser.UserId.Returns(caller);
        currentUser.Role.Returns((string?)null);

        var sellers = Substitute.For<ISellerProfileRepository>();

        return new MarketplaceAuthorizationService(currentUser, sellers);
    }

    private static MediaAccessRequest Request(MarketplaceListing listing) => new()
    {
        Caller = Stranger,
        MediaId = MediaId.New(),
        OwnerType = MediaOwnerType.MarketplaceListing,
        OwnerId = listing.Id.Value,
        Role = MediaReferenceRoles.Gallery,
        RequestedLevel = MediaAccessLevel.Read,
        UploadedBy = Seller
    };
}
