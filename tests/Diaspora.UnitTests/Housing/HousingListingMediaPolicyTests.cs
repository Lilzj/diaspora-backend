using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Modules.Media.Extensibility.Access;
using Diaspora.SharedKernel.Identity;
using Diaspora.UnitTests.Housing.TestData;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Housing;

/// <summary>
/// Whether a prospective tenant may look at a property's photographs.
/// </summary>
/// <remarks>
/// The twin of the marketplace policy's tests, and the same defect: on device, a published
/// property showed <em>"NO PHOTOS OF THIS PROPERTY"</em> while its photograph sat in
/// <c>media_assets</c>, because a media asset is private by default and nothing could tell Media
/// that a published listing's gallery is not. UAT-121.
/// </remarks>
public sealed class HousingListingMediaPolicyTests
{
    private static readonly UserId Landlord = UserId.New();
    private static readonly UserId Tenant = UserId.New();

    [Fact]
    public async Task A_published_propertys_gallery_is_readable_by_a_prospective_tenant()
    {
        var listing = HousingFixtures.PublishedListing();

        var decision = await Policy(listing, caller: Tenant).EvaluateAsync(Request(listing));

        decision.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task A_floor_plan_travels_with_the_listing()
    {
        // Deliberately in the public role list. A floor plan is why a tenant clicks through; it
        // is part of the advertisement rather than paperwork about it.
        var listing = HousingFixtures.PublishedListing();

        var decision = await Policy(listing, caller: Tenant)
            .EvaluateAsync(Request(listing) with { Role = MediaReferenceRoles.FloorPlan });

        decision.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task A_draft_propertys_gallery_is_not_readable()
    {
        var listing = HousingFixtures.Listing();

        var decision = await Policy(listing, caller: Tenant).EvaluateAsync(Request(listing));

        decision.IsAbstention.Should().BeTrue();
    }

    [Fact]
    public async Task A_tenancy_document_attached_to_a_published_listing_stays_private()
    {
        // The other half of the floor-plan decision. An EPC certificate, a tenancy agreement or
        // proof of ownership does not become public because the property did.
        var listing = HousingFixtures.PublishedListing();

        var decision = await Policy(listing, caller: Tenant)
            .EvaluateAsync(Request(listing) with { Role = MediaReferenceRoles.Document });

        decision.IsAbstention.Should().BeTrue();
    }

    [Fact]
    public async Task Seeing_a_property_does_not_confer_the_right_to_change_its_pictures()
    {
        var listing = HousingFixtures.PublishedListing();

        var decision = await Policy(listing, caller: Tenant)
            .EvaluateAsync(Request(listing) with { RequestedLevel = MediaAccessLevel.Manage });

        decision.IsAbstention.Should().BeTrue();
    }

    [Fact]
    public async Task It_never_denies()
    {
        // It can only widen, so nobody loses access they had before this policy existed.
        var listings = Substitute.For<IHousingListingRepository>();
        listings.GetForReadAsync(Arg.Any<HousingListingId>(), Arg.Any<CancellationToken>())
            .Returns((HousingListing?)null);

        var policy = new HousingListingMediaPolicy(listings, Authorization(Tenant));
        var listing = HousingFixtures.PublishedListing();

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
    public void It_speaks_only_for_housing_listings()
    {
        Policy(HousingFixtures.PublishedListing(), Tenant)
            .OwnerType.Should().Be(MediaOwnerType.HousingListing);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static HousingListingMediaPolicy Policy(HousingListing listing, UserId caller)
    {
        var listings = Substitute.For<IHousingListingRepository>();
        listings.GetForReadAsync(Arg.Any<HousingListingId>(), Arg.Any<CancellationToken>())
            .Returns(listing);

        return new HousingListingMediaPolicy(listings, Authorization(caller));
    }

    private static HousingAuthorizationService Authorization(UserId caller)
    {
        var currentUser = Substitute.For<Diaspora.Platform.Services.Abstractions.ICurrentUser>();
        currentUser.UserId.Returns(caller);
        currentUser.Role.Returns((string?)null);

        var landlords = Substitute.For<ILandlordProfileRepository>();

        return new HousingAuthorizationService(currentUser, landlords);
    }

    private static MediaAccessRequest Request(HousingListing listing) => new()
    {
        Caller = Tenant,
        MediaId = MediaId.New(),
        OwnerType = MediaOwnerType.HousingListing,
        OwnerId = listing.Id.Value,
        Role = MediaReferenceRoles.Gallery,
        RequestedLevel = MediaAccessLevel.Read,
        UploadedBy = Landlord
    };
}
