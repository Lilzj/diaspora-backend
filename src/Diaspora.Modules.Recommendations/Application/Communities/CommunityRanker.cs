using Diaspora.Contracts.CommunityProgramme.V1.Directory;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Infrastructure.Explainability;

namespace Diaspora.Modules.Recommendations.Application.Communities;

/// <summary>
/// Orders joinable communities for one member and says why each is there.
/// <para>
/// <b>Presence is the country signal.</b> A diaspora community is <em>from</em> one place (its origin)
/// and <em>active</em> in others (its presence); the country the app sends is the one the member is
/// looking at, so it is matched against presence. The member's own interest in a community's origin
/// country — learnt from what they read — is the strongest personal signal, and a shared language the
/// next. Member counts are never used: the directory does not report them as authoritative, and a
/// ranking built on them would imply a popularity nobody measured.
/// </para>
/// <para>Pure, so the order and the reasons can be pinned by unit tests.</para>
/// </summary>
public static class CommunityRanker
{
    public const string StrategyKey = "community-presence";

    private const decimal Base = 0.30m;
    private const decimal PresenceWeight = 0.30m;
    private const decimal OriginInterestWeight = 0.30m;
    private const decimal LanguageWeight = 0.10m;

    public static IReadOnlyList<RankedCommunity> Rank(CommunityRankingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var ranked = new List<RankedCommunity>();

        foreach (var community in input.Candidates)
        {
            if (input.Suppressed.Contains(community.CommunityId))
            {
                continue;
            }

            var score = Base;
            var reason = RecommendationReasonCode.StartHere;
            string? detail = null;
            var strongest = 0m;

            var present = input.CountryCode is { } country
                          && community.PresenceCountryCodes.Contains(country, StringComparer.OrdinalIgnoreCase);

            if (present)
            {
                score += PresenceWeight;
                (reason, detail, strongest) = (RecommendationReasonCode.PresentInCountry, input.CountryCode, PresenceWeight);
            }

            if (input.CountryInterest is { } interest
                && community.OriginCountryCode is { } origin
                && !string.Equals(origin, input.CountryCode, StringComparison.OrdinalIgnoreCase)
                && Math.Clamp(interest(origin), 0m, 1m) is var affinity and > 0m)
            {
                var contribution = OriginInterestWeight * affinity;
                score += contribution;

                // Ties go to the more personal signal: interest the member showed says more about
                // them than where a community happens to be.
                if (contribution >= strongest)
                {
                    (reason, detail, strongest) = (RecommendationReasonCode.MovingToCountry, origin, contribution);
                }
            }

            if (input.LanguageCode is { } language
                && (string.Equals(community.PrimaryLanguage, language, StringComparison.OrdinalIgnoreCase)
                    || community.SupportedLanguages.Contains(language, StringComparer.OrdinalIgnoreCase)))
            {
                score += LanguageWeight;

                if (strongest == 0m)
                {
                    (reason, detail) = (RecommendationReasonCode.SpeaksLanguage, null);
                }
            }

            ranked.Add(new RankedCommunity(
                community,
                decimal.Round(Math.Clamp(score, 0m, 1m), 6),
                reason,
                RecommendationReasonCatalog.TextFor(reason, detail),
                present));
        }

        return
        [
            .. ranked
                .OrderByDescending(item => item.Score)
                .ThenByDescending(item => item.Community.CreatedAt)
                .ThenBy(item => item.Community.CommunityId)
                .Take(Math.Max(1, input.Limit))
        ];
    }
}

public sealed record CommunityRankingInput
{
    public required IReadOnlyList<CommunitySuggestionCandidate> Candidates { get; init; }

    /// <summary>The presence country being looked at, upper case, or null.</summary>
    public string? CountryCode { get; init; }

    /// <summary>The member's learnt interest in a country, 0–1. Null when personalization is off.</summary>
    public Func<string, decimal>? CountryInterest { get; init; }

    /// <summary>The member's language. Null when unknown or personalization is off.</summary>
    public string? LanguageCode { get; init; }

    public required IReadOnlySet<Guid> Suppressed { get; init; }

    public int Limit { get; init; } = 20;
}

public sealed record RankedCommunity(
    CommunitySuggestionCandidate Community,
    decimal Score,
    RecommendationReasonCode ReasonCode,
    string ReasonText,
    bool IsPresentInCountry);
