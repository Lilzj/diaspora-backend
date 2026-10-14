using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Visibility;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Community;

/// <summary>
/// Audience evaluation, and the one direction it must never fail in.
/// </summary>
public class VisibilityTests
{
    private static readonly Guid Author = Guid.NewGuid();

    private static readonly ContentReference Content =
        ContentReference.Create(CommunityContentType.Post, Guid.NewGuid());

    [Fact]
    public void Public_content_is_visible_to_a_signed_out_reader()
    {
        Evaluate(ContentAudience.Public, viewer: null).Denied.Should().BeFalse();
    }

    [Fact]
    public void Members_only_content_is_hidden_from_a_signed_out_reader()
    {
        Evaluate(ContentAudience.Members, viewer: null).Denied.Should().BeTrue();
    }

    [Fact]
    public void Verified_only_content_is_hidden_from_an_unverified_member()
    {
        Evaluate(ContentAudience.Verified, UserId.New(), verified: false)
            .Denied.Should().BeTrue();

        Evaluate(ContentAudience.Verified, UserId.New(), verified: true)
            .Denied.Should().BeFalse();
    }

    [Fact]
    public void Country_content_reaches_a_reader_who_is_there_or_verified_as_a_resident()
    {
        Evaluate(ContentAudience.ForCountry("NG"), UserId.New(), country: "NG")
            .Denied.Should().BeFalse();

        Evaluate(ContentAudience.ForCountry("NG"), UserId.New(), verifiedCountries: ["NG"])
            .Denied.Should().BeFalse();

        Evaluate(ContentAudience.ForCountry("NG"), UserId.New(), country: "GB")
            .Denied.Should().BeTrue();
    }

    [Fact]
    public void Follower_content_reaches_a_follower_and_nobody_else()
    {
        Evaluate(ContentAudience.Followers, UserId.New(), follows: true).Denied.Should().BeFalse();
        Evaluate(ContentAudience.Followers, UserId.New(), follows: false).Denied.Should().BeTrue();
    }

    [Fact]
    public void An_author_always_sees_their_own_content_however_narrow_the_audience()
    {
        var author = UserId.From(Author);

        Evaluate(ContentAudience.Private, author).Denied.Should().BeFalse(
            "a member who cannot see their own removed post cannot appeal it");
    }

    [Fact]
    public void A_block_beats_every_audience_including_public()
    {
        Evaluate(ContentAudience.Public, UserId.New(), blocked: true).Denied.Should().BeTrue();
    }

    [Fact]
    public void A_custom_audience_is_abstained_on_rather_than_guessed_at()
    {
        var rule = new AudienceVisibilityEvaluator().Evaluate(Request(
            ContentAudience.ForRule(
                Diaspora.Modules.Community.Domain.Identifiers.VisibilityRuleId.New()),
            UserId.New()));

        rule.Abstained.Should().BeTrue();
    }

    [Fact]
    public void Content_nothing_allowed_is_denied_rather_than_shown()
    {
        // The safety property: unregistering the module that understood an audience hides the
        // content rather than publishing it.
        var registry = new VisibilityEvaluatorRegistry([]);

        registry.Evaluate(Request(ContentAudience.Public, UserId.New()))
            .Denied.Should().BeTrue();
    }

    [Fact]
    public void One_denial_beats_any_number_of_allowances()
    {
        var registry = new VisibilityEvaluatorRegistry(
            [new AudienceVisibilityEvaluator(), new AlwaysDenies()]);

        registry.IsVisible(Request(ContentAudience.Public, UserId.New())).Should().BeFalse();
    }

    private sealed class AlwaysDenies : IVisibilityEvaluator
    {
        public int Order => 10;

        public VisibilityDecision Evaluate(VisibilityRequest request) =>
            VisibilityDecision.Deny("test");
    }

    /// <summary>
    /// A neighbourhood audience is a radius around the post's own pin since 14 Oct 2026, measured
    /// against the reader's active location — no catalogue area on either side.
    /// </summary>
    [Fact]
    public void Neighborhood_content_reaches_a_reader_within_the_radius_of_its_pin_and_nobody_further()
    {
        var radius = AudienceVisibilityEvaluator.NeighborhoodRadiusKm;

        NeighborhoodDecision(contentPinned: true, distanceKm: radius - 0.1).Denied.Should().BeFalse();
        NeighborhoodDecision(contentPinned: true, distanceKm: radius + 0.1).Denied.Should().BeTrue();

        // A reader with no active location cannot be near anything pinned.
        NeighborhoodDecision(contentPinned: true, distanceKm: null).Denied.Should().BeTrue();

        // Never a signed-out reader, however near.
        NeighborhoodDecision(contentPinned: true, distanceKm: 0.1, signedIn: false)
            .Denied.Should().BeTrue();
    }

    [Fact]
    public void Neighborhood_content_with_no_pin_falls_back_to_its_country()
    {
        NeighborhoodDecision(contentPinned: false, distanceKm: null, viewerCountry: "KE")
            .Denied.Should().BeFalse();

        NeighborhoodDecision(contentPinned: false, distanceKm: null, viewerCountry: "GB")
            .Denied.Should().BeTrue();

        NeighborhoodDecision(
                contentPinned: false, distanceKm: null, viewerCountry: "GB", verifiedCountries: ["KE"])
            .Denied.Should().BeFalse("a verified resident of the country is of it, wherever they are");
    }

    private static VisibilityDecision NeighborhoodDecision(
        bool contentPinned,
        double? distanceKm,
        bool signedIn = true,
        string? viewerCountry = "KE",
        IReadOnlyCollection<string>? verifiedCountries = null) =>
        new AudienceVisibilityEvaluator().Evaluate(new VisibilityRequest
        {
            Content = Content,
            AuthorId = Author,
            Audience = ContentAudience.FromLevel(VisibilityLevel.Neighborhood),
            ViewerId = signedIn ? UserId.New() : null,
            ViewerCountryCode = viewerCountry,
            ViewerVerifiedCountries = verifiedCountries ?? [],
            ContentCountryCode = "KE",
            ContentLatitude = contentPinned ? -4.0435 : null,
            ContentLongitude = contentPinned ? 39.6682 : null,
            ViewerLatitude = distanceKm is null ? null : -4.05,
            ViewerLongitude = distanceKm is null ? null : 39.67,
            ViewerDistanceKm = distanceKm
        });

    private static VisibilityDecision Evaluate(
        ContentAudience audience,
        UserId? viewer,
        bool verified = false,
        string? country = null,
        IReadOnlyCollection<string>? verifiedCountries = null,
        bool follows = false,
        bool blocked = false) =>
        new AudienceVisibilityEvaluator().Evaluate(
            Request(audience, viewer, verified, country, verifiedCountries, follows, blocked));

    private static VisibilityRequest Request(
        ContentAudience audience,
        UserId? viewer,
        bool verified = false,
        string? country = null,
        IReadOnlyCollection<string>? verifiedCountries = null,
        bool follows = false,
        bool blocked = false) =>
        new()
        {
            Content = Content,
            AuthorId = Author,
            Audience = audience,
            ViewerId = viewer,
            ViewerIsVerified = verified,
            ViewerCountryCode = country,
            ViewerVerifiedCountries = verifiedCountries ?? [],
            ViewerFollowsAuthor = follows,
            IsBlocked = blocked
        };
}
