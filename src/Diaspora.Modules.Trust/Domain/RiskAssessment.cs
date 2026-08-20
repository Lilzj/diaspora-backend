using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Trust.Domain;

/// <summary>
/// A point-in-time read of how risky a member looks.
/// <para>
/// A value object, not an entity: a risk score is a derived opinion about evidence, so it is
/// recomputed rather than mutated, and two assessments with the same factors are the same
/// assessment. Storing it on the profile is a cache of the last computation, which is why it
/// carries its own timestamp.
/// </para>
/// <para>
/// This is staff-facing only. The trust centre renders it behind an "Admin view" label and
/// the public projection omits it entirely — telling a member their exact fraud score is an
/// invitation to tune their behaviour against it.
/// </para>
/// </summary>
public sealed class RiskAssessment : ValueObject
{
    /// <summary>Where a member starts before any signal is counted.</summary>
    public const int BaseScore = 10;

    public const int MediumThreshold = 30;
    public const int HighThreshold = 60;
    public const int MaxScore = 100;

    /// <summary>0–100. Matches the frontend's <c>RiskFactors.scoreValue</c>.</summary>
    public int Score { get; }

    public RiskLevel Level { get; }

    public IReadOnlyList<RiskFactor> Factors { get; }

    public DateTimeOffset AssessedAt { get; }

    private RiskAssessment(int score, RiskLevel level, IReadOnlyList<RiskFactor> factors, DateTimeOffset assessedAt)
    {
        Score = score;
        Level = level;
        Factors = factors;
        AssessedAt = assessedAt;
    }

    /// <summary>A member with no signals against them.</summary>
    public static RiskAssessment Baseline(DateTimeOffset assessedAt) =>
        new(BaseScore, RiskLevel.Low, [], assessedAt);

    /// <summary>
    /// Builds an assessment from weighted factors.
    /// <para>
    /// Weights live in <see cref="WeightOf"/> rather than being passed in, so that two
    /// callers cannot score the same factor differently and produce assessments that are not
    /// comparable — which is the failure mode that makes a risk queue useless.
    /// </para>
    /// </summary>
    public static RiskAssessment From(IEnumerable<RiskFactor> factors, DateTimeOffset assessedAt)
    {
        ArgumentNullException.ThrowIfNull(factors);

        var distinct = factors.Distinct().OrderBy(factor => factor).ToList();
        var score = Math.Min(MaxScore, BaseScore + distinct.Sum(WeightOf));

        var level = score switch
        {
            > HighThreshold => RiskLevel.High,
            > MediumThreshold => RiskLevel.Medium,
            _ => RiskLevel.Low
        };

        return new RiskAssessment(score, level, distinct.AsReadOnly(), assessedAt);
    }

    /// <summary>
    /// What each signal is worth. Mirrors the frontend's <c>calculateRiskScore</c> weights so
    /// the admin panel's number does not change the day the computation moves server-side.
    /// </summary>
    private static int WeightOf(RiskFactor factor) => factor switch
    {
        RiskFactor.Reports_Spike => 30,
        RiskFactor.Failed_Verification => 25,
        RiskFactor.VPN_Detected => 20,
        RiskFactor.Duplicate_Content => 20,
        RiskFactor.Device_Change => 15,
        RiskFactor.Rapid_Posting => 10,
        _ => 0
    };

    /// <summary>Whether this assessment should hold publishing until a human looks.</summary>
    public bool WarrantsReview => Level == RiskLevel.High;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Score;
        yield return Level;

        foreach (var factor in Factors)
        {
            yield return factor;
        }
    }
}
