using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Policies;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Analytics;

/// <summary>
/// The columns on one item's row and page — 13 Sep 2026.
/// <para>
/// <b>Found on the device.</b> Aisha's content table read "Saves 0 · Helpful votes 0 · Comments 0 ·
/// Shares 0 · Engagement 0.0%" on each of her listings and jobs. Nothing records any of those for a
/// listing or a job: saves, reactions, comments and shares reach analytics only from the Community
/// module, which the app offers on guides and community updates — and, for saves alone, on provider
/// profiles. Five permanent zeros per row,
/// indistinguishable from content nobody cares about. The owner dashboards had already left them out.
/// </para>
/// </summary>
public sealed class ContentItemSetTests
{
    [Theory]
    [InlineData(AnalyticsContentType.Marketplace)]
    [InlineData(AnalyticsContentType.Housing)]
    [InlineData(AnalyticsContentType.Job)]
    [InlineData(AnalyticsContentType.Event)]
    public void Listings_homes_jobs_and_events_show_their_views_and_their_one_action(AnalyticsContentType type)
    {
        AnalyticsMetricCatalog.ContentItemFor(type).Should().Equal(
            AnalyticsMetricCatalog.ContentViews, AnalyticsMetricCatalog.Conversion);
    }

    [Theory]
    [InlineData(AnalyticsContentType.Guide)]
    [InlineData(AnalyticsContentType.Post)]
    public void Guides_and_updates_keep_community_engagement_and_drop_the_action_they_never_ask_for(
        AnalyticsContentType type)
    {
        AnalyticsMetricCatalog.ContentItemFor(type).Should().Equal(
            AnalyticsMetricCatalog.ContentViews,
            AnalyticsMetricCatalog.Saves,
            AnalyticsMetricCatalog.HelpfulVotes,
            AnalyticsMetricCatalog.Comments,
            AnalyticsMetricCatalog.Shares,
            AnalyticsMetricCatalog.EngagementRate);
    }

    [Fact]
    public void A_provider_shows_its_views_and_its_saves()
    {
        // A provider's save button goes through Community, which publishes the save. Until the
        // provider dashboard, the row showed views alone and hid every one of them.
        AnalyticsMetricCatalog.ContentItemFor(AnalyticsContentType.Provider).Should().Equal(
            AnalyticsMetricCatalog.ContentViews, AnalyticsMetricCatalog.Saves);
    }

    [Fact]
    public void An_organisation_shows_its_views()
    {
        // Saving an organisation writes to the Bookmarks module, which publishes nothing.
        AnalyticsMetricCatalog.ContentItemFor(AnalyticsContentType.Organization)
            .Should().Equal(AnalyticsMetricCatalog.ContentViews);
    }

    [Theory]
    [InlineData(AnalyticsContentType.Marketplace)]
    [InlineData(AnalyticsContentType.Housing)]
    [InlineData(AnalyticsContentType.Job)]
    [InlineData(AnalyticsContentType.Event)]
    [InlineData(AnalyticsContentType.Guide)]
    [InlineData(AnalyticsContentType.Post)]
    [InlineData(AnalyticsContentType.Provider)]
    [InlineData(AnalyticsContentType.Organization)]
    public void An_item_shows_a_conversion_exactly_when_its_action_has_a_name(AnalyticsContentType type)
    {
        // Two rules that must agree: the columns an item shows, and what its conversions are called.
        // A kind with a named action and no conversion column would hide real sales; a conversion column
        // on a kind whose action has no name would be a permanent zero.
        AnalyticsMetricCatalog.ContentItemFor(type).Contains(AnalyticsMetricCatalog.Conversion)
            .Should().Be(AnalyticsConversionNames.For(type) is not null);
    }

    [Fact]
    public void Every_per_item_set_is_drawn_from_the_catalogue_superset()
    {
        foreach (var type in Enum.GetValues<AnalyticsContentType>())
        {
            AnalyticsMetricCatalog.ContentItemFor(type).Should().BeSubsetOf(AnalyticsMetricCatalog.ContentItem);
        }
    }
}
