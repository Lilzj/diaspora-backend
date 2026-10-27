namespace Diaspora.Modules.Trust.Domain;

/// <summary>
/// Which evidence counts toward a member's risk, and for how long (RWD-14, CS-4b; step 3f, Q7 and Q8).
/// <para>
/// <b>Two factors have evidence today, and only both together are High.</b> The weights are
/// <see cref="RiskAssessment"/>'s and unchanged: a report spike is 30 and a failed verification 25 on
/// a base of 10, so either alone is Medium and both are 65, above the High threshold of 60. One bad
/// week or one failed check is not enough to hold anything.
/// </para>
/// <para>
/// In code rather than configuration, for the reason the weights are: two deployments counting the
/// same evidence differently produce assessments nobody can compare.
/// </para>
/// </summary>
public static class RiskPolicy
{
    /// <summary>How long a fraud-like rejection counts.</summary>
    public static readonly TimeSpan VerificationWindow = TimeSpan.FromDays(30);

    /// <summary>The window a report spike is counted over.</summary>
    public static readonly TimeSpan ReportWindow = TimeSpan.FromDays(7);

    /// <summary>Pieces of content with an upheld report, within the window, that make a spike.</summary>
    public const int ReportedContentForSpike = 3;

    /// <summary>Evidence older than this counts toward nothing, and the risk sweep removes it.</summary>
    public static TimeSpan LongestWindow => VerificationWindow;

    /// <summary>
    /// Whether a rejection counts against the member. Only the reasons a reviewer chooses when they
    /// judge the evidence false: a blurred photo or an expired passport is not fraud.
    /// </summary>
    public static bool CountsAgainstMember(VerificationRejectionReason reason) =>
        reason is VerificationRejectionReason.SUSPECTED_FRAUD
            or VerificationRejectionReason.FACE_MATCH_FAILED
            or VerificationRejectionReason.DUPLICATE_SUBMISSION;

    /// <summary>
    /// The member's risk from the evidence on record.
    /// <para>
    /// A rejection counts while it is within thirty days and unanswered: a later grant of the same
    /// verification type answers it. A spike is upheld reports against three different pieces of the
    /// member's content within seven days.
    /// </para>
    /// </summary>
    public static RiskAssessment Assess(
        IEnumerable<RiskSignal> signals,
        IReadOnlyCollection<VerificationType> grants,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(signals);
        ArgumentNullException.ThrowIfNull(grants);

        var evidence = signals.ToList();
        var factors = new List<RiskFactor>();

        var unansweredRejection = evidence.Any(signal =>
            signal.Kind == RiskSignalKind.VerificationRejected
            && signal.OccurredAt > now - VerificationWindow
            && signal.VerificationType is { } type
            && !grants.Contains(type));

        if (unansweredRejection)
        {
            factors.Add(RiskFactor.Failed_Verification);
        }

        var reportedContent = evidence
            .Where(signal =>
                signal.Kind == RiskSignalKind.ReportUpheld
                && signal.OccurredAt > now - ReportWindow)
            .Select(signal => signal.ContentKey)
            .Distinct(StringComparer.Ordinal)
            .Count();

        if (reportedContent >= ReportedContentForSpike)
        {
            factors.Add(RiskFactor.Reports_Spike);
        }

        return RiskAssessment.From(factors, now);
    }
}
