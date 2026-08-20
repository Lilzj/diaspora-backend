using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.ValueObjects;

namespace Diaspora.Modules.Community.Extensibility.Moderation;

/// <summary>
/// Decides what a report or a review should cause, before a human sees it.
/// <para>
/// Separate from the review provider, because detecting and deciding are different jobs with
/// different owners. A classifier says "this looks like spam with 0.8 confidence"; a policy says
/// "spam over 0.9 hides immediately, spam over 0.6 queues, in this country everything queues".
/// The second is a deployment's decision, and coupling it to a model means changing the policy
/// requires changing the model.
/// </para>
/// <para>
/// Returning null means no automatic action, which is the correct default and what B6's own
/// threshold policy returns for almost everything.
/// </para>
/// </summary>
public interface IModerationPolicyProvider
{
    int Order => 0;

    /// <summary>The action to take automatically, or null to leave it to a human.</summary>
    ModerationActionType? Evaluate(ModerationSignal signal);
}
