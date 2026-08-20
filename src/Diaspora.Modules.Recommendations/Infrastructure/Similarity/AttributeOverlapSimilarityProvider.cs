using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Extensibility.Similarity;

namespace Diaspora.Modules.Recommendations.Infrastructure.Similarity;

/// <summary>
/// How alike two things are, by what they have in common.
/// <para>
/// <b>The deterministic stand-in for an embedding, and it is deliberately unsophisticated.</b>
/// Attributes, category, location and language, weighted and overlapped. It will never notice that
/// "flat" and "apartment" are the same word, which is exactly what an embedding is for — and the
/// point of this class is that the strategy above it does not care which of the two is answering.
/// </para>
/// <para>
/// <b>The weights encode what actually makes two things feel similar on this platform.</b> Category
/// carries the most, because "another two-bedroom rental" is the match a member recognises;
/// attributes next; location less, because a member browsing housing already filtered by location
/// and rewarding it again double-counts; language least, because it is closer to a hard requirement
/// than a similarity.
/// </para>
/// <para>
/// <b>Scored against the best single reference rather than the mean.</b> A member with eight recent
/// engagements across three categories would have every candidate dragged towards the middle by an
/// average, and the strongest match — the one that made the recommendation worth making — would be
/// indistinguishable from a weak one.
/// </para>
/// </summary>
public sealed class AttributeOverlapSimilarityProvider : ISimilarityProvider
{
    public const string ProviderKey = "attribute-overlap";

    private const decimal CategoryWeight = 0.4m;
    private const decimal AttributeWeight = 0.35m;
    private const decimal LocationWeight = 0.15m;
    private const decimal LanguageWeight = 0.1m;

    public string Key => ProviderKey;

    public Task<IReadOnlyList<decimal>> ScoreAsync(
        IReadOnlyList<RecommendationCandidate> candidates,
        IReadOnlyList<RecommendationCandidate> references,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(references);

        if (references.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<decimal>>(candidates.Select(_ => 0m).ToList());
        }

        // Attribute sets are built once per reference rather than per comparison. At four hundred
        // candidates by eight references that is eight allocations instead of thirty-two hundred.
        var referenceSets = references
            .Select(reference => (Candidate: reference, Attributes: AttributeSet(reference)))
            .ToList();

        var scores = new List<decimal>(candidates.Count);

        foreach (var candidate in candidates)
        {
            var candidateAttributes = AttributeSet(candidate);
            var best = 0m;

            foreach (var (reference, referenceAttributes) in referenceSets)
            {
                // A thing is not similar to itself in any useful sense — it is the same thing, and
                // the dismissal and engagement filters have their own reasons for excluding it.
                if (reference.Target == candidate.Target)
                {
                    continue;
                }

                var score = Compare(candidate, candidateAttributes, reference, referenceAttributes);

                if (score > best)
                {
                    best = score;
                }
            }

            scores.Add(decimal.Round(best, 6));
        }

        return Task.FromResult<IReadOnlyList<decimal>>(scores);
    }

    private static decimal Compare(
        RecommendationCandidate candidate,
        IReadOnlySet<string> candidateAttributes,
        RecommendationCandidate reference,
        IReadOnlySet<string> referenceAttributes)
    {
        var score = 0m;

        if (Matches(candidate.CategoryKey, reference.CategoryKey))
        {
            score += CategoryWeight;
        }

        score += AttributeWeight * Infrastructure.Ranking.RankingMath.Overlap(
            candidateAttributes, referenceAttributes);

        if (Matches(candidate.NeighborhoodName, reference.NeighborhoodName))
        {
            score += LocationWeight;
        }
        else if (Matches(candidate.CityName, reference.CityName))
        {
            score += LocationWeight * 0.6m;
        }
        else if (Matches(candidate.CountryCode, reference.CountryCode))
        {
            score += LocationWeight * 0.3m;
        }

        if (Matches(candidate.LanguageCode, reference.LanguageCode))
        {
            score += LanguageWeight;
        }

        return Math.Clamp(score, 0m, 1m);
    }

    /// <summary>
    /// A candidate's attributes as a comparable set.
    /// <para>
    /// The kind is folded into the key so a tag <c>senior</c> and a skill <c>senior</c> do not match
    /// each other — they are drawn from different vocabularies and mean different things.
    /// </para>
    /// </summary>
    private static HashSet<string> AttributeSet(RecommendationCandidate candidate) =>
        candidate.Attributes
            .Select(attribute => $"{attribute.Kind}:{attribute.Key}")
            .ToHashSet(StringComparer.Ordinal);

    private static bool Matches(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
