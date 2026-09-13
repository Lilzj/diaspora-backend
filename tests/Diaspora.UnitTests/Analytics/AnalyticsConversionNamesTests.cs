using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Policies;
using Diaspora.Modules.Analytics.Domain.Rollups;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Analytics;

/// <summary>
/// What a conversion is called, for one kind of content — 13 Sep 2026.
/// <para>
/// Owner dashboards were renamed first — "Sales", "Viewings", "Applications" — and one item's page
/// and one item's row in the content table still said the catalogue's "Actions taken" for the same
/// figure. The names now come from one rule keyed by content type, used by all three, so a listing's
/// sales cannot be "Sales" on the seller dashboard and "Actions taken" on the listing's own row.
/// </para>
/// </summary>
public sealed class AnalyticsConversionNamesTests
{
    [Theory]
    [InlineData(AnalyticsContentType.Marketplace, "Sales", "Conversion rate")]
    [InlineData(AnalyticsContentType.Housing, "Viewings", "Viewing rate")]
    [InlineData(AnalyticsContentType.Job, "Applications", "Application rate")]
    [InlineData(AnalyticsContentType.Event, "RSVPs", "RSVP rate")]
    public void Content_whose_action_has_a_name_is_given_it(AnalyticsContentType type, string count, string rate)
    {
        var names = AnalyticsConversionNames.For(type);

        names.Should().NotBeNull();
        names!.Count.Should().Be(count);
        names.Rate.Should().Be(rate);
    }

    [Theory]
    [InlineData(AnalyticsContentType.Guide)]
    [InlineData(AnalyticsContentType.Post)]
    [InlineData(AnalyticsContentType.Provider)]
    [InlineData(AnalyticsContentType.Organization)]
    [InlineData(AnalyticsContentType.Unknown)]
    public void Content_that_asks_for_nothing_keeps_the_general_name(AnalyticsContentType type)
    {
        AnalyticsConversionNames.For(type).Should().BeNull();
    }

    [Theory]
    [InlineData("a purchase for a marketplace listing")]
    [InlineData("a viewing for a housing listing")]
    [InlineData("an application for a job")]
    [InlineData("an RSVP for an event")]
    public void Every_named_action_is_the_one_the_served_definition_describes(string action)
    {
        // The tile's name and the sentence a member reads when they tap it must be about the same act.
        AnalyticsMetricCatalog.Find(AnalyticsMetricCatalog.Conversion)!.Definition.Should().Contain(action);
    }

    [Fact]
    public void Naming_a_jobs_set_renames_its_conversions_and_nothing_else()
    {
        var set = ContentItemSet();

        var named = AnalyticsMetricAssembler.NameConversions(set, AnalyticsContentType.Job);

        named.Select(metric => metric.Key).Should().Equal(set.Select(metric => metric.Key));
        named.Single(metric => metric.Key == AnalyticsMetricCatalog.Conversion).Label.Should().Be("Applications");

        foreach (var metric in named.Where(metric => metric.Key != AnalyticsMetricCatalog.Conversion))
        {
            metric.Label.Should().Be(AnalyticsMetricCatalog.Find(metric.Key)!.Label, "only the conversion is renamed");
        }

        named.Single(metric => metric.Key == AnalyticsMetricCatalog.Conversion).Definition
            .Should().Be(AnalyticsMetricCatalog.Find(AnalyticsMetricCatalog.Conversion)!.Definition);
    }

    [Fact]
    public void Naming_a_guides_set_changes_nothing()
    {
        var set = ContentItemSet();

        var named = AnalyticsMetricAssembler.NameConversions(set, AnalyticsContentType.Guide);

        named.Should().BeSameAs(set);
    }

    [Fact]
    public void Owner_dashboards_take_their_board_word_from_the_same_rule()
    {
        AnalyticsOwnerDatasetProfile.Seller.ConversionLabel.Should().Be("Sales");
        AnalyticsOwnerDatasetProfile.Landlord.ConversionLabel.Should().Be("Viewings");
        AnalyticsOwnerDatasetProfile.Employer.ConversionLabel.Should().Be("Applications");
    }

    private static IReadOnlyList<Diaspora.Modules.Analytics.Contracts.V1.Responses.CreatorMetricResponse> ContentItemSet()
    {
        var counts = new AnalyticsCounts(20, 2, 1, 1, 0, 0, 3, 0, 0, 0);
        var totals = new AnalyticsWindowTotals(counts, 5, 0, HasData: true, HasHistory: true);

        return AnalyticsMetricAssembler.BuildSet(AnalyticsMetricCatalog.ContentItem, totals, totals);
    }
}
