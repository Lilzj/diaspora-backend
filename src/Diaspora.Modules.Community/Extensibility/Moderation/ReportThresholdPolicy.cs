using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Policies;

namespace Diaspora.Modules.Community.Extensibility.Moderation;

/// <summary>
/// The one automatic rule B6 ships: enough open reports hides content pending review.
/// <para>
/// A blunt instrument and a known brigading vector, which is why the threshold is high enough to
/// be expensive to organise, the action is the reversible one, and a single moderator decision
/// undoes it. The alternative — leaving genuinely harmful content up until a human is free — is
/// worse in precisely the cases that matter most.
/// </para>
/// <para>
/// A rejected automated review also hides, because a provider that says reject has already made
/// a decision somebody configured it to make. A <em>flagged</em> review does nothing: flagged
/// means "worth a look", and hiding on suspicion would hand a classifier the power to silence.
/// </para>
/// </summary>
public sealed class ReportThresholdPolicy : IModerationPolicyProvider
{
    public int Order => 100;

    public ModerationActionType? Evaluate(ModerationSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);

        if (signal.ReviewStatus == ContentReviewStatus.Rejected)
        {
            return ModerationActionType.Hide;
        }

        return signal.OpenReportCount >= CommunityLimits.AutoHideReportThreshold
            ? ModerationActionType.Hide
            : null;
    }
}
