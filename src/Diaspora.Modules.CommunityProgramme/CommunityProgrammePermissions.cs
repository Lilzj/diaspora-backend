namespace Diaspora.Modules.CommunityProgramme;

/// <summary>
/// The permissions the Community Programme module owns.
/// <para>
/// Colon-separated keys with the module prefix, per the platform convention (policies use dots, see
/// <see cref="CommunityProgrammePolicies"/>). Tier floors live with the definitions in
/// <c>Authorization/CommunityProgrammeContributors</c>.
/// </para>
/// <para>
/// <b>A permission here is a necessary condition and never a sufficient one.</b> Every write in this
/// module is additionally gated on a capability the caller holds <em>in the organization it names</em>,
/// and that capability comes from B12's role edge. <c>communities:programme:manage</c> says the
/// caller may run a programme somewhere; whether they may run <em>this</em> one is a different
/// question, asked separately, on every request.
/// </para>
/// <para>
/// <b>The split: taking part is Limited, running something is Verified.</b> Applying for a
/// membership, offering to volunteer and voting in a poll are how somebody new finds their people.
/// Running a programme, deciding an application and publishing an announcement each put the caller
/// in authority over other members, which is the progression B2.0.1 gates on proven identity.
/// </para>
/// </summary>
public static class CommunityProgrammePermissions
{
    private const string Prefix = "communities";

    /// <summary>Read programmes, opportunities, polls and announcements the caller may see.</summary>
    public const string Read = Prefix + ":programme:read";

    /// <summary>Apply, vote, withdraw, cancel — the member's own acts.</summary>
    public const string Participate = Prefix + ":programme:participate";

    /// <summary>Run a membership programme: tiers, decisions, the member roll.</summary>
    public const string ManageProgramme = Prefix + ":programme:manage";

    /// <summary>Run volunteering: opportunities, decisions, assignments, confirmed hours.</summary>
    public const string ManageVolunteers = Prefix + ":volunteer:manage";

    /// <summary>Run polls: creation, closing, publishing results.</summary>
    public const string ManagePolls = Prefix + ":poll:manage";

    /// <summary>Publish an announcement to a scope.</summary>
    public const string PublishAnnouncements = Prefix + ":announcement:publish";

    /// <summary>Read an organization's analytics.</summary>
    public const string ViewAnalytics = Prefix + ":analytics:read";
}
