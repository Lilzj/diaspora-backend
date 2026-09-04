namespace Diaspora.Modules.CommunityProgramme;

/// <summary>
/// The wire-level error codes the community aggregate emits.
/// <para>
/// Under the same <c>communities</c> prefix as the rest of this module, and frozen for the same
/// reason: clients switch on them, support searches logs by them, and translations key off them.
/// Change a description freely; never change a code.
/// </para>
/// </summary>
public static class CommunityErrorCodes
{
    private const string Prefix = "communities.community";

    // ── Existence ───────────────────────────────────────────────────────────────────────────
    public const string NotFound = Prefix + ".not-found";

    // ── The slug ────────────────────────────────────────────────────────────────────────────
    public const string SlugRequired = Prefix + ".slug.required";
    public const string SlugLength = Prefix + ".slug.length";
    public const string SlugShape = Prefix + ".slug.shape";
    public const string SlugReserved = Prefix + ".slug.reserved";
    public const string SlugNotDerivable = Prefix + ".slug.not-derivable";
    public const string SlugTaken = Prefix + ".slug.taken";

    // ── Identity ────────────────────────────────────────────────────────────────────────────
    public const string NameRequired = Prefix + ".name.required";
    public const string NameLength = Prefix + ".name.length";
    public const string OriginRequired = Prefix + ".origin.required";
    public const string CountryCodeShape = Prefix + ".country-code.shape";

    // ── Lifecycle ───────────────────────────────────────────────────────────────────────────
    // The directory's orderings. See GetCommunityDirectoryQueryHandler.ReadSort.
    public const string SortUnknown = Prefix + ".sort.unknown";
    public const string SortByMembersUnavailable = Prefix + ".sort.members-unavailable";
    public const string SortByNameUnavailable = Prefix + ".sort.name-unavailable";

    public const string NotJoinable = Prefix + ".not-joinable";
    public const string AlreadyActive = Prefix + ".already-active";
    public const string ArchivedIsFinal = Prefix + ".archived-is-final";
    public const string SuspensionNeedsReason = Prefix + ".suspension.needs-reason";

    // ── Membership ──────────────────────────────────────────────────────────────────────────
    public const string UnknownMembershipVisibility = Prefix + ".membership.unknown-visibility";
    public const string UnknownMembershipAction = Prefix + ".membership.unknown-action";
    public const string AlreadyMember = Prefix + ".membership.already-member";
    public const string NotAMember = Prefix + ".membership.not-a-member";
    public const string MembershipRevoked = Prefix + ".membership.revoked";
    public const string InviteRequired = Prefix + ".membership.invite-required";
    public const string PrimaryRequiresMembership = Prefix + ".primary.requires-membership";
}
