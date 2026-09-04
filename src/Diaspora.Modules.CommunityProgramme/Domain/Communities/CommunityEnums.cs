namespace Diaspora.Modules.CommunityProgramme.Domain.Communities;

/// <summary>
/// What kind of community this is.
/// <para>
/// <b>The kind is not the origin.</b> A <see cref="NationalDiaspora"/> has a country of origin; a
/// <see cref="CityChapter"/> has a city of presence; an <see cref="InterestCommunity"/> may have
/// neither. Keeping them separate is what lets the directory filter by "Nigerians anywhere" and
/// "anyone in Manchester" without the two questions fighting.
/// </para>
/// </summary>
public enum CommunityType
{
    /// <summary>People from one country, wherever they now live.</summary>
    NationalDiaspora = 1,

    /// <summary>People from a region that is not a country — West Africa, the Caribbean.</summary>
    RegionalDiaspora = 2,

    /// <summary>A shared people or language rather than a shared passport.</summary>
    EthnoLinguistic = 3,

    /// <summary>A faith community.</summary>
    FaithCommunity = 4,

    /// <summary>A shared interest, with no geographic claim at all.</summary>
    InterestCommunity = 5,

    /// <summary>One city's chapter of a larger community.</summary>
    CityChapter = 6,
}

/// <summary>
/// Where a community is in its life.
/// <para>
/// <b>The register asked for "a lifecycle, so a suspended or archived community is
/// distinguishable from a deleted one".</b> All five states below are visible states — a deleted
/// community is not one of them, because a community is never hard-deleted. Archived is the end of
/// the line, and it stays readable so the links that pointed at it still resolve to an explanation
/// rather than a 404.
/// </para>
/// </summary>
public enum CommunityStatus
{
    /// <summary>Proposed and not yet open. Readable by its creator and by staff.</summary>
    Pending = 1,

    /// <summary>Open, and the only state in which somebody may join.</summary>
    Active = 2,

    /// <summary>
    /// Stopped by staff. Members keep their standing and see the reason; nobody new may join.
    /// Reversible, which is the whole difference from <see cref="Archived"/>.
    /// </summary>
    Suspended = 3,

    /// <summary>Closed for good. Readable, not joinable, and never reopened.</summary>
    Archived = 4,

    /// <summary>
    /// The platform cannot currently say. Not a stored state — returned when a community is known
    /// to exist but its record could not be read, so a caller can tell "we do not know" from "it is
    /// not there".
    /// </summary>
    Unavailable = 5,
}

/// <summary>
/// Who may see a community, and who may see that it exists at all.
/// <para>
/// <b>The register asked for "a visibility model, so an unlisted or private community is not
/// returned to a caller who may not see it".</b> The distinction that carries the weight is between
/// <see cref="Unlisted"/> and <see cref="Private"/>: an unlisted community is fully readable by
/// anyone holding its link, and is merely kept out of the directory. A private one is readable only
/// by its members, and to everybody else answers exactly as a community that does not exist.
/// </para>
/// </summary>
public enum CommunityVisibility
{
    /// <summary>Listed in the directory and readable by anyone, signed in or not.</summary>
    Public = 1,

    /// <summary>Readable by anyone with the link. Never returned by directory search.</summary>
    Unlisted = 2,

    /// <summary>Readable by members only. Indistinguishable from absent to everybody else.</summary>
    Private = 3,

    /// <summary>Private, and joinable only on an invitation the platform issued.</summary>
    InviteOnly = 4,
}

/// <summary>
/// One person's standing in one community.
/// <para>
/// <b>Ten states, and the three refusal states are distinct on purpose.</b> <see cref="Declined"/>
/// is the community saying no to a request; <see cref="Revoked"/> is the community removing
/// somebody who was in; <see cref="Left"/> is the member's own decision. Collapsing them would make
/// "you were removed" and "you left" the same sentence on somebody's screen.
/// </para>
/// </summary>
public enum CommunityMembershipState
{
    /// <summary>No relationship. The state an unauthenticated caller always sees.</summary>
    None = 0,

    /// <summary>Looked at it, and the platform noted that. Carries no standing.</summary>
    Discovering = 1,

    /// <summary>Said they are interested, before any request to join.</summary>
    Interested = 2,

    /// <summary>Follows the community's content without belonging to it.</summary>
    Following = 3,

    /// <summary>Asked to join a community that requires approval, and is waiting.</summary>
    PendingApproval = 4,

    /// <summary>A member.</summary>
    Member = 5,

    /// <summary>A member whose connection to the community has been verified.</summary>
    VerifiedMember = 6,

    /// <summary>Asked to join and was refused.</summary>
    Declined = 7,

    /// <summary>Was a member and was removed.</summary>
    Revoked = 8,

    /// <summary>Was a member and chose to leave.</summary>
    Left = 9,
}

/// <summary>
/// A role inside one community.
/// <para>
/// <b>A role is not an authorization, and the client says so in as many words: "Display only.
/// Authority comes from <c>capabilities</c>."</b> These drive badges and navigation; every action is
/// gated on the capability set the server evaluates, and no screen derives one from the other. Two
/// separate things because they drift — a moderator who is removed keeps their badge until the app
/// refetches, and must lose the affordance immediately.
/// </para>
/// <para>
/// Nothing grants any of these yet. The role directory is its own register entry
/// (FRONTEND-PHASE13-GAP-017) and its own increment; until it lands, every membership carries
/// <see cref="Member"/> alone and the moderation capabilities are false for everyone — which is
/// true, rather than a placeholder.
/// </para>
/// </summary>
public enum CommunityMembershipRole
{
    /// <summary>Belongs. Held by every current member, and implied rather than granted.</summary>
    Member = 1,

    /// <summary>Reviews reports and moderates content.</summary>
    Moderator = 2,

    /// <summary>Represents the community. A public-facing role, not a moderation one.</summary>
    Ambassador = 3,

    /// <summary>Runs the community: rules, roles, notices.</summary>
    Administrator = 4,
}

/// <summary>
/// Who may see that somebody belongs to a community.
/// <para>
/// <b><see cref="Private"/> is zero, and that is the whole design.</b> §41 requires that joining
/// publish nothing and that the member opt <em>in</em> to being visible. A missing or defaulted
/// value must therefore read as private — so private is what <c>default</c> gives, and any path that
/// forgets to set this fails closed rather than open.
/// </para>
/// <para>
/// Community membership is genuinely sensitive. A diaspora community, a faith community or a
/// support group can disclose nationality, immigration status, religion or health, and the platform
/// has no business publishing any of them because somebody tapped Join.
/// </para>
/// </summary>
public enum CommunityMembershipVisibility
{
    /// <summary>Nobody but the member. The default, deliberately at zero.</summary>
    Private = 0,

    /// <summary>Other members of the same community.</summary>
    CommunityOnly = 1,

    /// <summary>Anybody. Only ever by an explicit choice the member made.</summary>
    Public = 2,
}
