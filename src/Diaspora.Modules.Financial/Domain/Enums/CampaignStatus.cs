namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>
/// Where a fundraising campaign is in its life. Twelve states, and not one of them is a boolean.
/// <para>
/// <b>The register asked for exactly these twelve and the client already renders all of them.</b>
/// <c>DonationCause.IsActive</c> stays what it always was — whether B15 will take money for that
/// cause — and this is the programme around it. Collapsing the two would mean a campaign under
/// review and a campaign whose deadline passed were the same thing, and the member is told something
/// materially different in each.
/// </para>
/// </summary>
public enum CampaignStatus
{
    /// <summary>Being written. Visible to its organizer and to nobody else.</summary>
    Draft = 0,

    /// <summary>Submitted, waiting for a decision.</summary>
    PendingReview = 1,

    /// <summary>Refused, with a reason. Fixable — it can go back to draft.</summary>
    Rejected = 2,

    /// <summary>Approved and collecting. The cause exists from this moment.</summary>
    Published = 3,

    /// <summary>Collecting, and has been running.</summary>
    Active = 4,

    /// <summary>Temporarily not collecting, at the organizer's request.</summary>
    Paused = 5,

    /// <summary>Past its target and still collecting, because people give past a target on purpose.</summary>
    GoalReached = 6,

    /// <summary>Past its deadline. Not collecting.</summary>
    DeadlineReached = 7,

    /// <summary>Finished, and the money was used. Terminal.</summary>
    Completed = 8,

    /// <summary>Giving the money back. Not collecting.</summary>
    Refunding = 9,

    /// <summary>Called off. Terminal.</summary>
    Cancelled = 10,

    /// <summary>Shut, without a claim about how it ended. Terminal.</summary>
    Closed = 11
}

/// <summary>
/// What a campaign is for. The client's own eleven, in the client's own order.
/// <para>
/// A campaign category is not a <c>DonationCause</c> concept and deliberately does not become one:
/// causes are reference data with an <c>AcceptedKinds</c> list, and a category is how a member
/// browses. Storing it here keeps the browse filter on the aggregate that has a lifecycle.
/// </para>
/// </summary>
public enum CampaignCategory
{
    Medical = 0,
    Education = 1,
    Emergency = 2,
    Funeral = 3,
    CommunityProject = 4,
    Religious = 5,
    Sports = 6,
    LegalSupport = 7,
    Repatriation = 8,
    DisasterRelief = 9,
    Other = 10
}
