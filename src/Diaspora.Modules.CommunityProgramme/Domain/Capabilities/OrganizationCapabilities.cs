namespace Diaspora.Modules.CommunityProgramme.Domain.Capabilities;

/// <summary>
/// What one caller may do inside one organization.
/// <para>
/// <b>Ten flags, served as data, and this is the keystone of the whole module.</b> Nine of B18's
/// write surfaces are gated on one of these, and every one of them is decided here — server-side,
/// on every request — rather than inferred from a role name the client was told. The client's copy
/// decides which buttons to draw; the refusal is the control.
/// </para>
/// <para>
/// <b>It is a projection of B12's role edge, not a second one.</b> The frontend register recorded
/// this gap as "there is no membership edge between a user and an organisation at all". That was
/// not true of the certified backend: <c>OrganizationMembership</c> has carried a
/// <c>(userId, organizationId, role)</c> triple with a six-rung ladder since B12 shipped. What was
/// genuinely missing is the vocabulary above it — nothing could answer "may this person approve
/// members" because no capability set existed, only a role. This type is that vocabulary, and
/// <see cref="OrganizationCapabilityMap"/> is the one place a role becomes one.
/// </para>
/// <para>
/// <b>The default is nothing.</b> <see cref="None"/> is what a caller with no role, no session or
/// an unresolvable organization gets, and every flag on it is false. A capability model whose
/// failure mode is "grant" is not a capability model.
/// </para>
/// </summary>
public sealed record OrganizationCapabilities
{
    public required Guid OrganizationId { get; init; }

    /// <summary>
    /// B12's role name, passed through verbatim, or null when the caller holds none.
    /// <para>
    /// A label for the client to render, and explicitly not the contract: the flags below are. The
    /// frontend asserts exhaustively that it never derives a capability from this string, and the
    /// server never reads it back.
    /// </para>
    /// </summary>
    public string? Role { get; init; }

    public bool CanManageMembers { get; init; }

    public bool CanApproveMembers { get; init; }

    public bool CanManageTiers { get; init; }

    public bool CanManageEvents { get; init; }

    public bool CanManageCampaigns { get; init; }

    public bool CanManageVolunteers { get; init; }

    public bool CanManagePolls { get; init; }

    public bool CanPublishAnnouncements { get; init; }

    public bool CanViewAnalytics { get; init; }

    public bool CanViewFinancials { get; init; }

    /// <summary>Every flag false. What a caller with no standing gets, and the safe direction.</summary>
    public static OrganizationCapabilities None(Guid organizationId) =>
        new() { OrganizationId = organizationId };

    /// <summary>Whether the caller holds any management capability at all.</summary>
    public bool HoldsAny =>
        CanManageMembers
        || CanApproveMembers
        || CanManageTiers
        || CanManageEvents
        || CanManageCampaigns
        || CanManageVolunteers
        || CanManagePolls
        || CanPublishAnnouncements
        || CanViewAnalytics
        || CanViewFinancials;
}

/// <summary>
/// The ten capability names, as the request layer refers to them.
/// <para>
/// An enum rather than ten methods, so a handler asking for one reads as a single guard call and a
/// new capability is one member rather than a new overload on every service.
/// </para>
/// </summary>
public enum OrganizationCapability
{
    ManageMembers = 0,
    ApproveMembers = 1,
    ManageTiers = 2,
    ManageEvents = 3,
    ManageCampaigns = 4,
    ManageVolunteers = 5,
    ManagePolls = 6,
    PublishAnnouncements = 7,
    ViewAnalytics = 8,
    ViewFinancials = 9
}

/// <summary>
/// The one place a B12 role becomes a capability set.
/// <para>
/// <b>The ladder is B12's and this module does not extend it.</b> B12 ships
/// <c>Member · Volunteer · Moderator · Organizer · Administrator · Owner</c>, ordered by authority
/// with deliberate numbering gaps. The frontend register named a <c>Treasurer</c> the platform does
/// not have and did not name the <c>Volunteer</c> it does; adding a rung to another module's
/// authorization ladder to match a client's illustration would be inventing product scope, and the
/// register itself says the ladder is "illustrative, not authoritative — the real contract is
/// <c>OrganizationCapabilities</c>". So the six real roles map onto the ten real flags, and the
/// divergence is documented rather than papered over.
/// </para>
/// <para>
/// <b>The consequence to know: financial reading is Administrator and above.</b> Without a Treasurer
/// rung there is no role that sees money and nothing else, so <see cref="OrganizationCapability.ViewFinancials"/>
/// and <see cref="OrganizationCapability.ManageCampaigns"/> sit with the two roles that already run
/// the body. A deployment that wants the separation adds the rung to B12, and this map gains one
/// line.
/// </para>
/// </summary>
public static class OrganizationCapabilityMap
{
    // B12's MembershipRole values, by name. Matched as strings because the directory contract
    // deliberately hands roles across the module boundary as names rather than as B12's enum — a
    // consumer that had to reference the enum would be coupled to B12's vocabulary.
    private const string Owner = "Owner";
    private const string Administrator = "Administrator";
    private const string Organizer = "Organizer";
    private const string Moderator = "Moderator";
    private const string Volunteer = "Volunteer";
    private const string Member = "Member";

    /// <summary>
    /// The capability set for a role name.
    /// <para>
    /// An unrecognised or absent name yields <see cref="OrganizationCapabilities.None"/>. Failing
    /// closed on a name nobody recognises is the only safe reading: the alternative grants a
    /// capability on the strength of a typo.
    /// </para>
    /// </summary>
    public static OrganizationCapabilities For(Guid organizationId, string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return OrganizationCapabilities.None(organizationId);
        }

        return role.Trim() switch
        {
            Owner => new OrganizationCapabilities
            {
                OrganizationId = organizationId,
                Role = Owner,
                CanManageMembers = true,
                CanApproveMembers = true,
                CanManageTiers = true,
                CanManageEvents = true,
                CanManageCampaigns = true,
                CanManageVolunteers = true,
                CanManagePolls = true,
                CanPublishAnnouncements = true,
                CanViewAnalytics = true,
                CanViewFinancials = true
            },

            Administrator => new OrganizationCapabilities
            {
                OrganizationId = organizationId,
                Role = Administrator,
                CanManageMembers = true,
                CanApproveMembers = true,
                CanManageTiers = true,
                CanManageEvents = true,
                CanManageCampaigns = true,
                CanManageVolunteers = true,
                CanManagePolls = true,
                CanPublishAnnouncements = true,
                CanViewAnalytics = true,
                CanViewFinancials = true
            },

            // Runs what happens, not who belongs and not the money.
            Organizer => new OrganizationCapabilities
            {
                OrganizationId = organizationId,
                Role = Organizer,
                CanManageEvents = true,
                CanManageVolunteers = true,
                CanManagePolls = true,
                CanPublishAnnouncements = true,
                CanViewAnalytics = true
            },

            // Handles content and the community's voice. Not the roll, not the money, not the work.
            Moderator => new OrganizationCapabilities
            {
                OrganizationId = organizationId,
                Role = Moderator,
                CanManagePolls = true,
                CanPublishAnnouncements = true
            },

            // Signed up to help at events. B12 grants attendance-taking and nothing else, and this
            // map does not quietly widen it into running the volunteer programme.
            Volunteer => new OrganizationCapabilities
            {
                OrganizationId = organizationId, Role = Volunteer
            },

            Member => new OrganizationCapabilities
            {
                OrganizationId = organizationId, Role = Member
            },

            _ => OrganizationCapabilities.None(organizationId)
        };
    }

    /// <summary>Whether a set grants a particular capability.</summary>
    public static bool Grants(
        this OrganizationCapabilities capabilities, OrganizationCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        return capability switch
        {
            OrganizationCapability.ManageMembers => capabilities.CanManageMembers,
            OrganizationCapability.ApproveMembers => capabilities.CanApproveMembers,
            OrganizationCapability.ManageTiers => capabilities.CanManageTiers,
            OrganizationCapability.ManageEvents => capabilities.CanManageEvents,
            OrganizationCapability.ManageCampaigns => capabilities.CanManageCampaigns,
            OrganizationCapability.ManageVolunteers => capabilities.CanManageVolunteers,
            OrganizationCapability.ManagePolls => capabilities.CanManagePolls,
            OrganizationCapability.PublishAnnouncements => capabilities.CanPublishAnnouncements,
            OrganizationCapability.ViewAnalytics => capabilities.CanViewAnalytics,
            OrganizationCapability.ViewFinancials => capabilities.CanViewFinancials,
            _ => false
        };
    }
}
