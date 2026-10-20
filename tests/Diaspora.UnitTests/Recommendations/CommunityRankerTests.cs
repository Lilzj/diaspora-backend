using Diaspora.Contracts.CommunityProgramme.V1.Directory;
using Diaspora.Modules.Recommendations.Application.Communities;
using Diaspora.Modules.Recommendations.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Recommendations;

/// <summary>
/// The communities rail: presence country first, then what the member has shown interest in, and a
/// reason that names the signal actually used.
/// </summary>
public class CommunityRankerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_community_present_in_the_country_outranks_one_that_is_not_and_says_where()
    {
        var inGb = Community("Ghanaians in London", presence: ["GB"], createdDaysAgo: 30);
        var inFr = Community("Ghanaians in Paris", presence: ["FR"], createdDaysAgo: 1);

        var ranked = CommunityRanker.Rank(Input([inFr, inGb], country: "GB"));

        ranked[0].Community.Should().Be(inGb, "presence in the country being looked at beats recency");
        ranked[0].ReasonCode.Should().Be(RecommendationReasonCode.PresentInCountry);
        ranked[0].ReasonText.Should().Be("Active in GB.");
        ranked[0].IsPresentInCountry.Should().BeTrue();

        ranked[1].ReasonCode.Should().Be(RecommendationReasonCode.StartHere);
    }

    [Fact]
    public void Interest_in_a_communitys_origin_is_the_reason_when_it_is_the_stronger_signal()
    {
        var nigerian = Community("Nigerians in London", presence: ["GB"], origin: "NG");
        var ghanaian = Community("Ghanaians in London", presence: ["GB"], origin: "GH");

        var ranked = CommunityRanker.Rank(Input(
            [ghanaian, nigerian],
            country: "GB",
            interest: code => code == "NG" ? 1m : 0m));

        ranked[0].Community.Should().Be(nigerian);
        ranked[0].ReasonCode.Should().Be(RecommendationReasonCode.MovingToCountry);
        ranked[0].ReasonText.Should().Be("Because you're interested in NG.");
    }

    [Fact]
    public void Interest_in_the_country_the_member_is_looking_at_is_not_counted_as_interest_in_an_origin()
    {
        var local = Community("Londoners", presence: ["GB"], origin: "GB");

        var ranked = CommunityRanker.Rank(Input([local], country: "GB", interest: _ => 1m));

        ranked[0].ReasonCode.Should().Be(RecommendationReasonCode.PresentInCountry);
    }

    [Fact]
    public void A_shared_language_lifts_a_community_and_is_the_reason_only_when_nothing_stronger_applies()
    {
        var french = Community("Francophones", presence: ["CA"], language: "fr");
        var english = Community("Anglophones", presence: ["CA"], language: "en");

        var ranked = CommunityRanker.Rank(Input([english, french], country: null, language: "fr"));

        ranked[0].Community.Should().Be(french);
        ranked[0].ReasonCode.Should().Be(RecommendationReasonCode.SpeaksLanguage);
    }

    [Fact]
    public void A_dismissed_community_is_left_out()
    {
        var dismissed = Community("Dismissed", presence: ["GB"]);
        var kept = Community("Kept", presence: ["GB"]);

        var ranked = CommunityRanker.Rank(Input([dismissed, kept], country: "GB", suppressed: [dismissed.CommunityId]));

        ranked.Should().ContainSingle().Which.Community.Should().Be(kept);
    }

    [Fact]
    public void The_limit_is_honoured_and_newer_communities_break_ties()
    {
        var older = Community("Older", presence: ["GB"], createdDaysAgo: 10);
        var newer = Community("Newer", presence: ["GB"], createdDaysAgo: 2);
        var newest = Community("Newest", presence: ["GB"], createdDaysAgo: 1);

        var ranked = CommunityRanker.Rank(Input([older, newer, newest], country: "GB", limit: 2));

        ranked.Select(item => item.Community.Name).Should().Equal("Newest", "Newer");
    }

    private static CommunityRankingInput Input(
        IReadOnlyList<CommunitySuggestionCandidate> candidates,
        string? country,
        Func<string, decimal>? interest = null,
        string? language = null,
        IReadOnlyCollection<Guid>? suppressed = null,
        int limit = 20) =>
        new()
        {
            Candidates = candidates,
            CountryCode = country,
            CountryInterest = interest,
            LanguageCode = language,
            Suppressed = (suppressed ?? []).ToHashSet(),
            Limit = limit
        };

    private static CommunitySuggestionCandidate Community(
        string name,
        IReadOnlyList<string> presence,
        string? origin = null,
        string? language = null,
        int createdDaysAgo = 5) =>
        new()
        {
            CommunityId = Guid.NewGuid(),
            Name = name,
            Slug = name.ToLowerInvariant().Replace(' ', '-'),
            Type = "NationalDiaspora",
            OriginCountryCode = origin,
            PresenceCountryCodes = presence,
            PrimaryLanguage = language,
            CreatedAt = Now.AddDays(-createdDaysAgo)
        };
}
