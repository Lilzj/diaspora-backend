using Diaspora.Modules.Financial.Domain.Enums;

namespace Diaspora.Modules.Financial.Domain.Policies;

/// <summary>
/// The campaign state machine, in one table.
/// <para>
/// <b>Transcribed from the frontend's <c>CAMPAIGN_TRANSITIONS</c>, member for member.</b> The client
/// holds a copy to decide which buttons to draw and the state-model document is explicit that it is
/// "not the authority" — this is. An illegal move answers 409 here, and the client's copy being
/// wrong or old changes nothing about what the server permits.
/// </para>
/// <para>
/// <b>Three states are terminal and lead nowhere:</b> <see cref="CampaignStatus.Completed"/>,
/// <see cref="CampaignStatus.Cancelled"/> and <see cref="CampaignStatus.Closed"/>. A campaign that
/// could be reopened out of a terminal state would be a campaign whose refunds might have already
/// gone out.
/// </para>
/// <para>
/// <b><see cref="CampaignStatus.GoalReached"/> still collects.</b> People give past a target
/// deliberately, and a platform that stopped taking money at 100% would be refusing gifts somebody
/// meant to make. <see cref="CampaignStatus.DeadlineReached"/> does not, because a deadline is a
/// promise about when.
/// </para>
/// </summary>
public static class CampaignTransitions
{
    private static readonly Dictionary<CampaignStatus, CampaignStatus[]> Table =
        new()
        {
            [CampaignStatus.Draft] = [CampaignStatus.PendingReview, CampaignStatus.Cancelled],
            [CampaignStatus.PendingReview] =
                [CampaignStatus.Published, CampaignStatus.Rejected, CampaignStatus.Cancelled],
            [CampaignStatus.Rejected] =
                [CampaignStatus.Draft, CampaignStatus.PendingReview, CampaignStatus.Cancelled],
            [CampaignStatus.Published] =
            [
                CampaignStatus.Active, CampaignStatus.Paused, CampaignStatus.Cancelled,
                CampaignStatus.Closed
            ],
            [CampaignStatus.Active] =
            [
                CampaignStatus.Paused, CampaignStatus.GoalReached, CampaignStatus.DeadlineReached,
                CampaignStatus.Closed, CampaignStatus.Refunding, CampaignStatus.Cancelled
            ],
            [CampaignStatus.Paused] =
                [CampaignStatus.Active, CampaignStatus.Closed, CampaignStatus.Cancelled],
            [CampaignStatus.GoalReached] =
                [CampaignStatus.Active, CampaignStatus.Completed, CampaignStatus.Closed],
            [CampaignStatus.DeadlineReached] =
                [CampaignStatus.Completed, CampaignStatus.Closed, CampaignStatus.Refunding],
            [CampaignStatus.Completed] = [],
            [CampaignStatus.Refunding] = [CampaignStatus.Closed],
            [CampaignStatus.Cancelled] = [],
            [CampaignStatus.Closed] = []
        };

    /// <summary>Whether the machine permits this move. Same state to same state is not asked here.</summary>
    public static bool CanTransition(CampaignStatus from, CampaignStatus to) =>
        Table.TryGetValue(from, out var allowed) && Array.IndexOf(allowed, to) >= 0;

    /// <summary>Every move legal from here. For the response's <c>canManage</c> surface and for tests.</summary>
    public static IReadOnlyList<CampaignStatus> From(CampaignStatus status) =>
        Table.TryGetValue(status, out var allowed) ? allowed : [];

    /// <summary>Nothing moves out of these.</summary>
    public static bool IsTerminal(CampaignStatus status) =>
        status is CampaignStatus.Completed or CampaignStatus.Cancelled or CampaignStatus.Closed;

    /// <summary>
    /// Whether money may be given to a campaign in this state.
    /// <para>
    /// <b>The server's answer, and the one <c>FundraisingCampaign.canDonate</c> carries.</b> A state
    /// alone is not enough — a campaign with no linked cause cannot collect whatever its state says,
    /// which is why the aggregate asks both.
    /// </para>
    /// </summary>
    public static bool AcceptsDonations(CampaignStatus status) =>
        status is CampaignStatus.Published or CampaignStatus.Active or CampaignStatus.GoalReached;

    /// <summary>
    /// Whether the organizer may still edit the campaign's own text.
    /// <para>
    /// Editable before it collects and while it is running; not once it has ended. Somebody who read
    /// a story and gave to it is entitled to have that story still be there.
    /// </para>
    /// </summary>
    public static bool IsEditable(CampaignStatus status) =>
        status is CampaignStatus.Draft
            or CampaignStatus.Rejected
            or CampaignStatus.PendingReview
            or CampaignStatus.Published
            or CampaignStatus.Active
            or CampaignStatus.Paused;

    /// <summary>Whether a campaign in this state should be visible to somebody who does not run it.</summary>
    public static bool IsPubliclyVisible(CampaignStatus status) =>
        status is not (CampaignStatus.Draft or CampaignStatus.PendingReview or CampaignStatus.Rejected);
}
