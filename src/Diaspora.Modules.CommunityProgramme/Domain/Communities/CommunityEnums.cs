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
