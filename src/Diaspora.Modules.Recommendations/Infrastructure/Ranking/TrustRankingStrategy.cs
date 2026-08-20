using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Ranking;

namespace Diaspora.Modules.Recommendations.Infrastructure.Ranking;

/// <summary>
/// How much the platform can vouch for a candidate.
/// <para>
/// <b>The strategy that matters most and is easiest to get wrong.</b> A diaspora member acting on a
/// recommendation is often committing money to a stranger in a country they have just arrived in;
/// a verified provider and an unverified one are not two points on a quality scale, they are two
/// different risks. Weighting trust like popularity would be a product failure with real
/// consequences for real people.
/// </para>
/// <para>
/// <b>It ranks, and never gates.</b> The temptation to exclude the unverified is constant and wrong
/// for two reasons: a platform in a country where verification has just launched would have an empty
/// marketplace, and exclusion is a policy's job — see <see cref="RecommendationPolicyKind"/>. A
/// deployment that genuinely wants verified-only on a surface configures the trust-restriction
/// policy, which is auditable, reversible and visible in the diagnostics. A strategy that quietly
/// scored the unverified to zero would achieve the same thing with none of those properties.
/// </para>
/// <para>
/// <b>Verification and quality are separate inputs because they answer different questions.</b>
/// Verified says the platform checked who they are; quality says other members were satisfied.
/// A verified provider with poor reviews and an unverified one with excellent reviews are both
/// worth showing, and collapsing the two into one number would hide which was which.
/// </para>
/// </summary>
public sealed class TrustRankingStrategy : IRankingStrategy
{
    public const string StrategyKey = "trust";

    /// <summary>What a verified candidate scores before quality is folded in.</summary>
    private const decimal VerifiedBase = 0.7m;

    /// <summary>What an unverified candidate scores before quality is folded in. Never zero.</summary>
    private const decimal UnverifiedBase = 0.25m;

    private const decimal QualityWeight = 0.3m;

    public string Key => StrategyKey;

    public RankingStrategyKind Kind => RankingStrategyKind.Trust;

    public bool SupportsColdStart => true;

    public Task<IReadOnlyList<RankingScore>> RankAsync(
        RankingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var scores = new List<RankingScore>(context.Candidates.Count);

        foreach (var candidate in context.Candidates)
        {
            var baseline = candidate.IsVerified ? VerifiedBase : UnverifiedBase;
            var value = decimal.Round(
                Math.Clamp(baseline + (candidate.QualityScore * QualityWeight), 0m, 1m), 6);

            // The reason leads with whichever claim is the stronger one. "Verified" is a fact the
            // platform established; "highly rated" is one other members established. Saying the
            // weaker of the two when the stronger applies would undersell the recommendation.
            var reason = candidate.IsVerified
                ? RecommendationReasonCode.VerifiedProvider
                : candidate.QualityScore >= 0.7m
                    ? RecommendationReasonCode.HighlyRated
                    : RecommendationReasonCode.Unspecified;

            scores.Add(new RankingScore
            {
                Value = value,
                ReasonCode = reason,
                SignalKinds = [SignalKind.Trust]
            });
        }

        return Task.FromResult<IReadOnlyList<RankingScore>>(scores);
    }
}
