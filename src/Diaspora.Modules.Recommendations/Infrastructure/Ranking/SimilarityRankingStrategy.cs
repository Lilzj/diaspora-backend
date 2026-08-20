using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Ranking;
using Diaspora.Modules.Recommendations.Extensibility.Similarity;

namespace Diaspora.Modules.Recommendations.Infrastructure.Ranking;

/// <summary>
/// How much a candidate resembles what the member has already engaged with.
/// <para>
/// <b>"More like the last thing you looked at" is the most immediately legible recommendation there
/// is, and the easiest to overdo.</b> A member who viewed one two-bedroom flat in Grand Baie should
/// see more two-bedroom flats in Grand Baie; a member who sees nothing else has been put in a loop.
/// The diversity pass is what stops the second, which is why these two are separate components and
/// why a deployment cannot turn one off without noticing the other.
/// </para>
/// <para>
/// <b>The comparison itself is delegated to <see cref="ISimilarityProvider"/>, and that delegation
/// is the whole point.</b> The shipped implementation is attribute overlap; an embedding-based one
/// is a registration swap. This strategy — what to be similar to, how many references to use, how to
/// explain it — does not change either way, which is what makes the AI seam a seam rather than a
/// promise.
/// </para>
/// <para>
/// <b>References come from what the member did, most recent first, capped small.</b> Twenty
/// references would make almost everything somewhat similar to something and the score would stop
/// discriminating; the cap keeps the comparison against what the member is doing now rather than
/// against everything they have ever done.
/// </para>
/// </summary>
public sealed class SimilarityRankingStrategy : IRankingStrategy
{
    public const string StrategyKey = "similarity";

    /// <summary>How many recent engagements to compare against.</summary>
    public const string ReferenceCountParameter = "referencecount";

    private const int DefaultReferenceCount = 8;

    private readonly ISimilarityProvider _similarity;
    private readonly IRecommendationCandidateRepository _candidates;

    public SimilarityRankingStrategy(
        ISimilarityProvider similarity, IRecommendationCandidateRepository candidates)
    {
        _similarity = similarity;
        _candidates = candidates;
    }

    public string Key => StrategyKey;

    public RankingStrategyKind Kind => RankingStrategyKind.Similarity;

    public bool SupportsColdStart => false;

    public async Task<IReadOnlyList<RankingScore>> RankAsync(
        RankingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var none = context.Candidates.Select(_ => RankingScore.None()).ToList();

        if (!context.Personalization.IsSurfacePersonalized || context.EngagedTargetKeys.Count == 0)
        {
            return none;
        }

        var referenceCount = (int)context.Configuration.ParameterOrDefault(
            ReferenceCountParameter, DefaultReferenceCount);

        var references = await LoadReferencesAsync(
            context, Math.Max(1, referenceCount), cancellationToken);

        if (references.Count == 0)
        {
            return none;
        }

        var similarities = await _similarity.ScoreAsync(
            context.Candidates, references, cancellationToken);

        var scores = new List<RankingScore>(context.Candidates.Count);

        for (var index = 0; index < context.Candidates.Count; index++)
        {
            var value = index < similarities.Count ? similarities[index] : 0m;

            if (value <= 0m)
            {
                scores.Add(RankingScore.None());
                continue;
            }

            var candidate = context.Candidates[index];

            scores.Add(new RankingScore
            {
                Value = decimal.Round(Math.Clamp(value, 0m, 1m), 6),
                ReasonCode = ChooseReason(candidate, references),
                ReasonDetail = candidate.CategoryKey,
                SignalKinds = [SignalKind.Viewed, SignalKind.Saved, SignalKind.Applied]
            });
        }

        return scores;
    }

    /// <summary>
    /// Loads the candidates behind the member's recent engagements.
    /// <para>
    /// Reads the projection rather than the owning modules — a similarity comparison needs the same
    /// attribute vocabulary on both sides, and the projection is the only place both exist.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<RecommendationCandidate>> LoadReferencesAsync(
        RankingContext context, int limit, CancellationToken cancellationToken)
    {
        var targets = context.EngagedTargetKeys
            .Select(ParseKey)
            .Where(parsed => parsed is not null)
            .Select(parsed => parsed!.Value)
            .Take(limit)
            .ToList();

        return targets.Count == 0
            ? []
            : await _candidates.ListByTargetsAsync(targets, cancellationToken);
    }

    /// <summary>
    /// Which "similar to" claim to make.
    /// <para>
    /// Says "you viewed similar" only when the reference set shares this candidate's type. A housing
    /// listing scored similar to a guide the member read is a real and useful match, but explaining
    /// it as "because you viewed similar housing" would be false.
    /// </para>
    /// </summary>
    private static RecommendationReasonCode ChooseReason(
        RecommendationCandidate candidate, IReadOnlyList<RecommendationCandidate> references)
    {
        var sameType = references.Any(reference =>
            reference.Target.TargetType == candidate.Target.TargetType);

        return sameType
            ? RecommendationReasonCode.ViewedSimilar
            : RecommendationReasonCode.MatchesInterests;
    }

    private static (RecommendationTargetType TargetType, Guid TargetId)? ParseKey(string key)
    {
        var separator = key.IndexOf(':', StringComparison.Ordinal);

        if (separator <= 0 || separator == key.Length - 1)
        {
            return null;
        }

        return Enum.TryParse<RecommendationTargetType>(key[..separator], out var targetType) &&
               Guid.TryParse(key[(separator + 1)..], out var targetId)
            ? (targetType, targetId)
            : null;
    }
}
