using Diaspora.Modules.Financial.Extensibility.Fraud;
using Diaspora.Modules.Financial.Options;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Infrastructure.Fraud;

/// <summary>
/// The shipped fraud assessor: two rules a person can read and an operator can tune.
/// <para>
/// <b>It refuses nothing and reviews rarely, on purpose.</b> Shipping a half-guessed heuristic would
/// misfire on exactly the members least able to argue with it — somebody paying for their first
/// listing from a country the platform sees little traffic from — and the platform recorded that
/// reasoning already when it declined to ship a spam-signal provider for B12.
/// </para>
/// <para>
/// The two rules: too many attempts in an hour, and an unverified member above a daily ceiling. Both
/// produce a <em>review</em> rather than a refusal, because a review costs somebody an inspection
/// and a refusal costs somebody their payment.
/// </para>
/// <para>
/// <b>This is the seam a B16 model lands on.</b> Assessors are additive and the strictest decision
/// wins, so a model runs alongside these rather than instead of them.
/// </para>
/// </summary>
public sealed class VelocityFraudAssessor : IFraudAssessor
{
    private readonly FinancialOptions _options;

    public VelocityFraudAssessor(IOptions<FinancialOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    public string Name => "velocity";

    public Task<FraudAssessment> AssessAsync(
        FraudContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var reasons = new List<string>(2);
        var score = 0;

        if (context.RecentAttemptCount >= _options.FraudVelocityAttemptsPerHour)
        {
            reasons.Add(
                $"{context.RecentAttemptCount} payment attempts in the last hour.");
            score += 40;
        }

        if (!context.IsVerified
            && context.RecentVolumeMinorUnits + context.Amount.MinorUnits
                > _options.FraudUnverifiedDailyCeilingMinorUnits)
        {
            reasons.Add("An unverified member above the daily ceiling.");
            score += 40;
        }

        return Task.FromResult(reasons.Count == 0
            ? FraudAssessment.Allow()
            : FraudAssessment.Review(score, [.. reasons]));
    }
}
