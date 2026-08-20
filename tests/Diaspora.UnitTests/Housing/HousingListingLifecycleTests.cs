using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.UnitTests.Housing.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Housing;

/// <summary>
/// The listing lifecycle, and the two places it differs from every other listing in the platform:
/// Published and Available are separate states, and Occupied is not terminal.
/// </summary>
public class HousingListingLifecycleTests
{
    private static readonly DateTimeOffset Now = HousingFixtures.Now;

    [Fact]
    public void A_new_listing_is_a_private_draft()
    {
        var listing = HousingFixtures.Listing();

        listing.Status.Should().Be(HousingListingStatus.Draft);
        listing.Visibility.Should().Be(HousingVisibility.Private);
        listing.PublishedAt.Should().BeNull();
    }

    /// <summary>
    /// The module's one readiness rule. A property with no photograph is a property members assume
    /// does not exist, so publication refuses it rather than shipping a card with a grey box.
    /// </summary>
    [Fact]
    public void A_listing_with_no_photograph_cannot_be_published()
    {
        var listing = HousingFixtures.Listing();

        listing.IsPublishable.Should().BeFalse();
        listing.Publish(Now).IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// The distinction that justifies two states: a flat advertised in March for a June tenancy is
    /// discoverable and not yet occupiable, and one status cannot say both.
    /// </summary>
    [Fact]
    public void Publishing_a_property_whose_move_in_date_has_not_arrived_lands_in_published()
    {
        var listing = HousingFixtures.Listing(availableFrom: Now.AddMonths(3));

        listing.AttachMedia(Guid.NewGuid(), HousingMediaRole.Cover, null, Now);

        listing.Publish(Now).IsSuccess.Should().BeTrue();

        listing.Status.Should().Be(HousingListingStatus.Published);
        listing.Visibility.Should().Be(HousingVisibility.Public);
    }

    [Fact]
    public void Publishing_a_property_that_is_ready_now_lands_in_available()
    {
        var listing = HousingFixtures.PublishedListing();

        listing.Status.Should().Be(HousingListingStatus.Available);
        listing.FirstPublishedAt.Should().Be(Now);
    }

    /// <summary>
    /// A property coming back after a tenancy is not a new one. Consumers notify differently on the
    /// two, and a member should not be told about a home they have already rejected.
    /// </summary>
    [Fact]
    public void Republishing_after_a_tenancy_is_not_a_first_publication()
    {
        var listing = HousingFixtures.PublishedListing();

        listing.MoveTo(HousingListingStatus.Unavailable, Now);
        listing.ClearDomainEvents();

        listing.MoveTo(HousingListingStatus.Available, Now.AddMonths(6));

        listing.FirstPublishedAt.Should().Be(
            Now, "the first publication never moves, which is what time-to-let is measured from");
    }

    /// <summary>
    /// The difference from B10: a sold bicycle is gone, and a let flat always comes back.
    /// </summary>
    [Fact]
    public void An_occupied_property_can_return_to_the_market()
    {
        HousingListingStatusTransitions
            .CanTransition(HousingListingStatus.Occupied, HousingListingStatus.Available)
            .Should().BeTrue("tenancies end, and re-letting is the normal case rather than the odd one");

        HousingListingStatusTransitions
            .IsTerminal(HousingListingStatus.Occupied)
            .Should().BeFalse();
    }

    /// <summary>
    /// Unavailable is the state a landlord redecorating for a fortnight needs, and it must not cost
    /// them their listing age or their view count.
    /// </summary>
    [Fact]
    public void Unavailable_returns_to_available_without_republication()
    {
        var listing = HousingFixtures.PublishedListing();

        listing.MakeUnavailable(Now, null, "Redecorating.").IsSuccess.Should().BeTrue();
        listing.Status.Should().Be(HousingListingStatus.Unavailable);

        listing.MoveTo(HousingListingStatus.Available, Now.AddDays(14))
            .IsSuccess.Should().BeTrue();

        listing.PublishedAt.Should().Be(Now, "it never stopped being published");
    }

    /// <summary>
    /// Archived returns only to Draft: a property that has been off the market wants its rent, its
    /// dates and its photographs looked at before anybody is shown it again.
    /// </summary>
    [Fact]
    public void An_archived_property_comes_back_as_a_draft_and_nothing_else()
    {
        HousingListingStatusTransitions.From(HousingListingStatus.Archived)
            .Should().BeEquivalentTo([HousingListingStatus.Draft]);
    }

    [Fact]
    public void Restoring_from_archive_makes_it_private_again()
    {
        var listing = HousingFixtures.PublishedListing();

        listing.Archive(Now, null, null);
        listing.RestoreFromArchive(Now.AddDays(1), null).IsSuccess.Should().BeTrue();

        listing.Status.Should().Be(HousingListingStatus.Draft);
        listing.Visibility.Should().Be(HousingVisibility.Private);
    }

    [Fact]
    public void An_archived_property_cannot_be_edited_until_it_is_restored()
    {
        var listing = HousingFixtures.PublishedListing();

        listing.Archive(Now, null, null);

        listing.SetTotalUnits(3, baseRevision: null)
            .IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// Viewings stay open on a reserved property, and reservations do not. Reservations fall
    /// through, and the second person in the queue is who the landlord calls when they do.
    /// </summary>
    [Fact]
    public void A_reserved_property_still_takes_viewings_but_not_reservations()
    {
        HousingListingStatusTransitions
            .AcceptsViewings(HousingListingStatus.Reserved).Should().BeTrue();

        HousingListingStatusTransitions
            .AcceptsReservations(HousingListingStatus.Reserved).Should().BeFalse();
    }

    /// <summary>
    /// Optimistic concurrency. A landlord and a moderator do edit the same listing, and an agency's
    /// staff genuinely do edit it from two desks.
    /// </summary>
    [Fact]
    public void A_save_against_a_stale_revision_is_refused()
    {
        var listing = HousingFixtures.Listing();
        var revision = listing.Revision;

        listing.SetTotalUnits(2, baseRevision: revision).IsSuccess.Should().BeTrue();

        listing.SetTotalUnits(3, baseRevision: revision)
            .IsFailure.Should().BeTrue("somebody else moved it on");
    }

    /// <summary>
    /// A member opening a listing while its landlord is editing it must not make the landlord's
    /// save fail.
    /// </summary>
    [Fact]
    public void Recording_a_view_does_not_bump_the_revision()
    {
        var listing = HousingFixtures.PublishedListing();
        var revision = listing.Revision;

        listing.RecordView(isUnique: true);

        listing.Revision.Should().Be(revision);
        listing.Engagement.ViewCount.Should().Be(1);
    }
}
