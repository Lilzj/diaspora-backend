using Diaspora.Modules.Analytics.Application.Consumers;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Policies;
using Diaspora.Modules.Analytics.Domain.Rollups;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Analytics;

/// <summary>
/// Enquiries on the seller, landlord and provider dashboards — 13 Sep 2026.
/// <para>
/// <b>What an enquiry is, pinned.</b> A conversation about a listing, a home or a provider profile in
/// which somebody wrote to the owner, counted once, when its first message is sent. Not when the
/// conversation opens: tapping "Message" opens one before anything is typed, and two of the first three
/// such conversations in production were never written in. Not a conversion either: "Sales" must never
/// count questions.
/// </para>
/// </summary>
public sealed class EnquiryAnalyticsTests
{
    // ── The definition ──────────────────────────────────────────────────────

    [Fact]
    public void An_enquiry_is_a_measured_count_with_a_definition_that_says_when_it_counts()
    {
        var definition = AnalyticsMetricCatalog.Find(AnalyticsMetricCatalog.Enquiries)!;

        definition.CatalogueKey.Should().Be("content.enquiries");
        definition.Label.Should().Be("Enquiries");
        definition.Format.Should().Be(AnalyticsMetricFormat.Count);
        definition.Availability.Should().Be(AnalyticsMetricAvailability.Available);
        definition.Trust.Should().Be(AnalyticsTrustClass.Server, "a client cannot report that somebody wrote");

        definition.Definition.Should().Contain("first message", "the moment it counts is the definition");
        definition.Definition.Should().Contain("never wrote in", "an empty chat is the case people will ask about");
        definition.Definition.Should().Contain("one you started");
    }

    [Fact]
    public void The_catalogue_version_moved_because_a_definition_was_added()
    {
        AnalyticsMetricCatalog.Version.Should().Be("1.4.0");
    }

    // ── Where it is served ──────────────────────────────────────────────────

    [Fact]
    public void Selling_letting_and_a_providers_dashboard_carry_enquiries()
    {
        AnalyticsMetricCatalog.SellerHeadline.Should().Contain(AnalyticsMetricCatalog.Enquiries);
        AnalyticsMetricCatalog.LandlordHeadline.Should().Contain(AnalyticsMetricCatalog.Enquiries);
        AnalyticsMetricCatalog.ProviderHeadline.Should().Contain(AnalyticsMetricCatalog.Enquiries);
    }

    [Fact]
    public void Hiring_and_a_creators_overview_do_not()
    {
        // A job's conversations are an employer and a candidate, usually started by the employer.
        AnalyticsMetricCatalog.EmployerHeadline.Should().NotContain(AnalyticsMetricCatalog.Enquiries);
        AnalyticsMetricCatalog.CreatorHeadline.Should().NotContain(AnalyticsMetricCatalog.Enquiries);
    }

    [Theory]
    [InlineData(AnalyticsContentType.Marketplace, true)]
    [InlineData(AnalyticsContentType.Housing, true)]
    [InlineData(AnalyticsContentType.Provider, true)]
    [InlineData(AnalyticsContentType.Job, false)]
    [InlineData(AnalyticsContentType.Event, false)]
    [InlineData(AnalyticsContentType.Guide, false)]
    [InlineData(AnalyticsContentType.Post, false)]
    [InlineData(AnalyticsContentType.Organization, false)]
    public void An_items_row_shows_enquiries_exactly_where_the_consumer_can_count_them(
        AnalyticsContentType type, bool shown)
    {
        // Two rules that must agree: the columns an item shows, and the conversations counted. A column
        // with no consumer behind it would be a permanent zero; a consumer with no column, invisible.
        var counted = new[] { "MarketplaceListing", "HousingListing", "ProviderInquiry" }
            .Select(ConversationFirstMessageAnalyticsConsumer.ContentTypeFor)
            .Contains(type);

        AnalyticsMetricCatalog.ContentItemFor(type).Contains(AnalyticsMetricCatalog.Enquiries)
            .Should().Be(shown).And.Be(counted);
    }

    // ── What the consumer counts ────────────────────────────────────────────

    [Theory]
    [InlineData("MarketplaceListing", AnalyticsContentType.Marketplace)]
    [InlineData("HousingListing", AnalyticsContentType.Housing)]
    [InlineData("ProviderInquiry", AnalyticsContentType.Provider)]
    [InlineData("providerinquiry", AnalyticsContentType.Provider)]
    public void A_conversation_about_a_listing_a_home_or_a_provider_is_an_enquiry(
        string contextType, AnalyticsContentType expected)
    {
        ConversationFirstMessageAnalyticsConsumer.ContentTypeFor(contextType).Should().Be(expected);
    }

    [Theory]
    [InlineData("JobOpportunity")]
    [InlineData("PrivateConversation")]
    [InlineData("CommunityEvent")]
    [InlineData("CommunityGuide")]
    [InlineData("SupportTicket")]
    [InlineData("BusinessAccount")]
    [InlineData("")]
    [InlineData(null)]
    public void Any_other_conversation_is_not(string? contextType)
    {
        // BusinessAccount included: nothing in the app opens one, so what its entity id names is unknown.
        ConversationFirstMessageAnalyticsConsumer.ContentTypeFor(contextType).Should().BeNull();
    }

    // ── The arithmetic ──────────────────────────────────────────────────────

    [Fact]
    public void Enquiries_sum_across_days_and_are_neither_an_engagement_nor_a_conversion()
    {
        var monday = Counts(views: 10, conversions: 1, enquiries: 2);
        var tuesday = Counts(views: 30, conversions: 2, enquiries: 5);

        var week = monday.Add(tuesday);

        week.Enquiries.Should().Be(7);
        week.Engagements.Should().Be(0, "an enquiry is a conversation, not a response to the content");

        var totals = new AnalyticsWindowTotals(week, 20, 0, HasData: true, HasHistory: true);

        AnalyticsMetricAssembler.ValueOf(AnalyticsMetricCatalog.Enquiries, totals).Should().Be(7);
        AnalyticsMetricAssembler.ValueOf(AnalyticsMetricCatalog.Conversion, totals).Should().Be(3, "sales stay sales");
        AnalyticsMetricAssembler.ValueOf(AnalyticsMetricCatalog.ConversionRate, totals)
            .Should().Be(7.5, "3 ÷ 40 × 100 — the enquiries are not in the rate");
    }

    [Fact]
    public void A_dashboard_naming_its_conversions_leaves_enquiries_alone()
    {
        var totals = new AnalyticsWindowTotals(Counts(views: 40, conversions: 3, enquiries: 7), 20, 0, true, true);

        var named = AnalyticsMetricAssembler.NameConversions(
            AnalyticsMetricAssembler.BuildSet(AnalyticsMetricCatalog.LandlordHeadline, totals, totals),
            AnalyticsContentType.Housing);

        named.Single(metric => metric.Key == AnalyticsMetricCatalog.Conversion).Label.Should().Be("Viewings");
        named.Single(metric => metric.Key == AnalyticsMetricCatalog.Enquiries).Label.Should().Be("Enquiries");
    }

    private static AnalyticsCounts Counts(long views, long conversions, long enquiries) =>
        new(views, 0, 0, 0, 0, 0, conversions, 0, 0, 0, enquiries);
}
