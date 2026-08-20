namespace Diaspora.Modules.CommunityProgramme.Domain.Enums;

/// <summary>
/// Where one member stands with one organization's membership programme.
/// <para>
/// <b>Fourteen states, and not one of them collapses.</b> The client renders fourteen different
/// screens off this value — a spinner and "we are confirming with your bank" for
/// <see cref="PaymentPending"/>, a retry for <see cref="PaymentFailed"/>, a price for
/// <see cref="RenewalDue"/> — and the register's state model documents each. Collapsing any pair
/// loses a screen.
/// </para>
/// <para>
/// <b><see cref="NotAMember"/>, <see cref="Eligible"/> and <see cref="Ineligible"/> are never
/// stored.</b> They are what the programme read answers for a caller who has no row, and the
/// distinction between the last two is the server's evaluation of the tier requirements. A stored
/// membership always starts at <see cref="Draft"/> or later.
/// </para>
/// </summary>
public enum ProgrammeMembershipStatus
{
    /// <summary>No row. Answered by the programme read, never persisted.</summary>
    NotAMember = 0,

    /// <summary>No row, and every requirement met. Answered by the programme read.</summary>
    Eligible = 1,

    /// <summary>No row, and something outstanding. Answered by the programme read.</summary>
    Ineligible = 2,

    /// <summary>Started and not sent.</summary>
    Draft = 3,

    /// <summary>Sent, and the organization has not decided.</summary>
    Pending = 4,

    /// <summary>Accepted. Anything owed is still owed.</summary>
    Approved = 5,

    /// <summary>Refused. Terminal.</summary>
    Rejected = 6,

    /// <summary>A payment is in flight. B15 owns whether it settles.</summary>
    PaymentPending = 7,

    /// <summary>The payment did not go through, and may be retried.</summary>
    PaymentFailed = 8,

    /// <summary>In, and current.</summary>
    Active = 9,

    /// <summary>
    /// Past the renewal date and inside the grace window.
    /// <para>
    /// <b>Still grants benefits.</b> Somebody one day late has not lost their membership, and
    /// taking it away early is the organization's decision to make through its grace period rather
    /// than a consequence of how the state was named.
    /// </para>
    /// </summary>
    RenewalDue = 10,

    /// <summary>The grace window passed. The member may rejoin, which creates a new record.</summary>
    Expired = 11,

    /// <summary>The organization suspended it. Reversible by the organization.</summary>
    Suspended = 12,

    /// <summary>The member ended it. Terminal.</summary>
    Cancelled = 13
}

/// <summary>Who moved a membership. Recorded on every history entry.</summary>
public enum MembershipActor
{
    Member = 0,
    Organization = 1,

    /// <summary>The lifecycle sweep. Expiry and renewal-due are nobody's decision.</summary>
    System = 2
}

/// <summary>How often a tier is charged. <c>OneOff</c> and <c>Lifetime</c> are charged once.</summary>
public enum BillingPeriod
{
    OneOff = 0,
    Monthly = 1,
    Quarterly = 2,
    Yearly = 3,
    Lifetime = 4
}

/// <summary>
/// Who may see a programme at all.
/// <para>
/// Enforced server-side by omission. A <see cref="Private"/> or <see cref="InviteOnly"/> programme
/// is <b>absent</b> from a discovery page for a caller who may not see it — not present with a flag,
/// because a flag means the private group already reached the device.
/// </para>
/// </summary>
public enum ProgrammeVisibility
{
    /// <summary>Listed in discovery and readable by anybody.</summary>
    Public = 0,

    /// <summary>Readable by anybody holding the link. Absent from discovery.</summary>
    Unlisted = 1,

    /// <summary>Readable only by the organization's own people.</summary>
    Private = 2,

    /// <summary>Readable only by the organization's own people, and joinable only by invitation.</summary>
    InviteOnly = 3
}

/// <summary>What kind of thing a tier asks of an applicant.</summary>
public enum MembershipRequirementKind
{
    Verification = 0,
    Residency = 1,
    Age = 2,
    Invitation = 3,
    Referral = 4,
    Document = 5,
    Other = 6
}

/// <summary>Where a volunteer opportunity is in its life.</summary>
public enum VolunteerOpportunityStatus
{
    Draft = 0,
    Published = 1,

    /// <summary>Every place taken. Applications are still accepted onto the waiting list.</summary>
    Full = 2,

    /// <summary>Closed to new applications. Existing assignments stand.</summary>
    Closed = 3,

    /// <summary>The work happened. Terminal.</summary>
    Completed = 4,

    /// <summary>Called off. Terminal.</summary>
    Cancelled = 5
}

/// <summary>
/// Where somebody's offer to volunteer stands.
/// <para>
/// <b><see cref="Waitlisted"/> is pending rather than terminal</b>, because a full opportunity
/// waitlists rather than refuses — the kinder of the two correct answers, and the one the register
/// asks for.
/// </para>
/// </summary>
public enum VolunteerApplicationStatus
{
    /// <summary>No row. Answered by the opportunity read, never persisted.</summary>
    NotApplied = 0,

    Draft = 1,
    Pending = 2,
    Approved = 3,
    Rejected = 4,
    Waitlisted = 5,
    Withdrawn = 6,
    Cancelled = 7,
    Completed = 8
}

/// <summary>Where an approved volunteer's placement stands.</summary>
public enum VolunteerAssignmentStatus
{
    Assigned = 0,
    Confirmed = 1,
    CheckedIn = 2,
    Completed = 3,
    NoShow = 4,
    Cancelled = 5
}

/// <summary>Where a poll is in its life.</summary>
public enum PollStatus
{
    Draft = 0,

    /// <summary>Published with a future opening moment. The sweep opens it.</summary>
    Scheduled = 1,

    Open = 2,
    Closed = 3,
    Cancelled = 4,

    /// <summary>Closed, and the numbers are public. Terminal.</summary>
    ResultsPublished = 5
}

/// <summary>How a poll is answered.</summary>
public enum PollVotingType
{
    SingleChoice = 0,
    MultipleChoice = 1,
    OpenResponse = 2
}

/// <summary>
/// Who may vote.
/// <para>
/// Evaluated server-side on every read and every write. A members-only poll answers 403 for a
/// non-member rather than being filtered out of a list the device already holds.
/// </para>
/// </summary>
public enum PollAudience
{
    Public = 0,
    OrganizationMembers = 1,
    Neighborhood = 2,
    Country = 3,

    /// <summary>Named individuals only. The list is the poll's.</summary>
    Invited = 4
}

/// <summary>What an announcement is addressed to.</summary>
public enum AnnouncementScope
{
    /// <summary>Everybody in a country. The platform-wide notice.</summary>
    Community = 0,

    /// <summary>An organization's own members.</summary>
    Organization = 1,

    /// <summary>A B4 area.</summary>
    Neighborhood = 2,

    /// <summary>The people who responded to a B12 event.</summary>
    Event = 3,

    /// <summary>The people who gave to a campaign. B15.1 owns the campaign itself.</summary>
    Campaign = 4
}

/// <summary>The window an analytics or impact read covers.</summary>
public enum AnalyticsPeriod
{
    Last7Days = 0,
    Last30Days = 1,
    Last90Days = 2,
    Last12Months = 3,
    AllTime = 4
}

/// <summary>What an impact figure is counted over.</summary>
public enum ImpactScope
{
    Member = 0,
    Organization = 1,
    Neighborhood = 2,
    Country = 3
}

/// <summary>The kinds of thing the federated Community Life search can return.</summary>
public enum CommunityEntityType
{
    Event = 0,
    Series = 1,
    Organization = 2,
    Membership = 3,
    Campaign = 4,
    Opportunity = 5,
    Poll = 6,
    Announcement = 7
}
