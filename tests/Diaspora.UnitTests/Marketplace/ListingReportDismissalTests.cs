using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.UnitTests.Marketplace.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Marketplace;

/// <summary>
/// What dismissing a report does to a listing (7 Nov 2026).
/// <para>
/// Dismissal called <c>Unhide</c>, which ignores a flag, so a listing that a moderator looked at and
/// found to be nothing stayed flagged and came back to the top of the queue. It now takes the
/// report's flag off, and nothing stronger: a hide or a suspension is a separate decision.
/// </para>
/// </summary>
public sealed class ListingReportDismissalTests
{
    [Fact]
    public void Dismissing_takes_the_reports_flag_off()
    {
        var listing = MarketplaceFixtures.ActiveListing();
        listing.Flag(null, "Automatically flagged by reports.", MarketplaceFixtures.Now);
        listing.ModerationState.Should().Be(ListingModerationState.Flagged);

        listing.ClearFlag(MarketplaceFixtures.NewUserId(), MarketplaceFixtures.Now).IsSuccess.Should().BeTrue();

        listing.ModerationState.Should().Be(ListingModerationState.Clear);
    }

    [Fact]
    public void Dismissing_leaves_a_suspension_where_it_is()
    {
        var listing = MarketplaceFixtures.ActiveListing();
        listing.Suspend("Counterfeit goods, confirmed.", MarketplaceFixtures.NewUserId(), MarketplaceFixtures.Now)
            .IsSuccess.Should().BeTrue();

        listing.ClearFlag(MarketplaceFixtures.NewUserId(), MarketplaceFixtures.Now).IsSuccess.Should().BeTrue();

        listing.ModerationState.Should().Be(ListingModerationState.Suspended);
    }
}
