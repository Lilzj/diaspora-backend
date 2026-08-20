using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Policies;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Analytics;

/// <summary>
/// The vocabulary translation, asserted against the vocabulary that produced it.
/// <para>
/// <b>This file exists because the failure it prevents is completely silent.</b> B6 emits its
/// content-type names camelCased; the client uses PascalCase; the Phase 15 route segment uses
/// lower-case and disagrees with B6 about what "listing" means. A translation that missed any of
/// those would map a real engagement onto <c>Unknown</c> — a row that still saves, still counts, and
/// never appears on anybody's dashboard. Nothing would fail; a creator's saves would simply be zero.
/// </para>
/// <para>
/// The B6 spellings below are produced by calling <c>CommunityWireNames.ToWire</c> rather than
/// written out, so a change to either module's vocabulary fails here rather than at runtime.
/// </para>
/// </summary>
public sealed class AnalyticsContentTypeTests
{
    /// <summary>
    /// Every content type B6 can publish an engagement about resolves — using B6's own wire form.
    /// </summary>
    [Theory]
    [InlineData(CommunityContentType.Post, AnalyticsContentType.Post)]
    [InlineData(CommunityContentType.Guide, AnalyticsContentType.Guide)]
    [InlineData(CommunityContentType.Listing, AnalyticsContentType.Provider)]
    [InlineData(CommunityContentType.Job, AnalyticsContentType.Job)]
    [InlineData(CommunityContentType.Housing, AnalyticsContentType.Housing)]
    [InlineData(CommunityContentType.Marketplace, AnalyticsContentType.Marketplace)]
    [InlineData(CommunityContentType.Event, AnalyticsContentType.Event)]
    [InlineData(CommunityContentType.Organization, AnalyticsContentType.Organization)]
    public void Every_community_content_type_resolves_from_its_own_wire_form(
        CommunityContentType published, AnalyticsContentType expected)
    {
        var wire = CommunityWireNames.ToWire(published);

        AnalyticsContentTypes.FromCommunity(wire).Should().Be(
            expected, "B6 publishes '{0}' and analytics must recognise it", wire);
    }

    /// <summary>
    /// <b>The word "listing" means two different things and both are right.</b> B6 calls a service
    /// provider a listing; the Phase 15 route calls a marketplace item one. A single translation
    /// table would attribute a provider's analytics to a marketplace seller, and it would look
    /// correct in every review.
    /// </summary>
    [Fact]
    public void The_word_listing_is_read_differently_by_each_vocabulary()
    {
        AnalyticsContentTypes.FromCommunity("listing").Should().Be(AnalyticsContentType.Provider);
        AnalyticsContentTypes.FromRouteSegment("listing").Should().Be(AnalyticsContentType.Marketplace);
    }

    /// <summary>The client's Phase 7 vocabulary round-trips through the route segment vocabulary.</summary>
    [Theory]
    [InlineData(AnalyticsContentType.Guide, "Guide", "guide")]
    [InlineData(AnalyticsContentType.Post, "Update", "update")]
    [InlineData(AnalyticsContentType.Event, "Event", "event")]
    [InlineData(AnalyticsContentType.Marketplace, "Marketplace", "listing")]
    [InlineData(AnalyticsContentType.Provider, "Provider", "provider")]
    [InlineData(AnalyticsContentType.Housing, "Housing", "housing")]
    [InlineData(AnalyticsContentType.Job, "Job", "job")]
    public void The_client_and_route_vocabularies_are_both_served(
        AnalyticsContentType type, string clientName, string routeSegment)
    {
        AnalyticsContentTypes.ToClientContentType(type).Should().Be(clientName);
        AnalyticsContentTypes.ToSubjectType(type).Should().Be(routeSegment);
        AnalyticsContentTypes.FromRouteSegment(routeSegment).Should().Be(type);
    }

    /// <summary>
    /// <b>An unrecognised name is <c>Unknown</c> rather than an exception.</b> A module that adds a
    /// content type must not be able to break ingestion for the ones that already work — a consumer
    /// that threw would be retried, fail again, and dead-letter a fact it could simply have stored
    /// under a type that says it does not know.
    /// </summary>
    [Theory]
    [InlineData("comment")]
    [InlineData("poll")]
    [InlineData("member")]
    [InlineData("something-a-later-module-invented")]
    [InlineData("")]
    [InlineData(null)]
    public void An_unrecognised_content_type_is_unknown_rather_than_a_failure(string? value)
    {
        AnalyticsContentTypes.FromCommunity(value).Should().Be(AnalyticsContentType.Unknown);
        AnalyticsContentTypes.FromRouteSegment(value).Should().Be(AnalyticsContentType.Unknown);
    }

    /// <summary>Every content type analytics serves has a route the client can open.</summary>
    [Fact]
    public void Every_served_content_type_has_a_client_route()
    {
        var id = Guid.NewGuid();

        foreach (var type in Enum.GetValues<AnalyticsContentType>()
            .Where(type => type != AnalyticsContentType.Unknown))
        {
            AnalyticsContentTypes.ToClientRoute(type, id).Should().NotBeNullOrWhiteSpace(
                "{0} appears on a content-performance row and must be openable", type);
        }
    }
}
