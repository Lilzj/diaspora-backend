using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Domain.Errors;

/// <summary>
/// Failures belonging to the community aggregate.
/// <para>
/// Separate from <see cref="CommunityProgrammeErrors"/> because they answer a different question. A
/// programme error is about what an organization offers; these are about the community itself —
/// whether it exists, whether this caller may see it, and whether it is in a state that can be
/// joined.
/// </para>
/// <para>
/// <b>"Not visible to you" and "does not exist" are deliberately the same error.</b> A private
/// community must be indistinguishable from an absent one, or the directory becomes an oracle for
/// probing which communities exist. The one exception is a caller who demonstrably holds the
/// community — a member hitting a route they lack the standing for — where a distinct refusal is
/// the honest answer and tells them nothing they did not already know.
/// </para>
/// </summary>
public static class CommunityErrors
{
    // ── Existence and visibility ────────────────────────────────────────────────────────────

    public static readonly Error NotFound = Error.NotFound(
        CommunityErrorCodes.NotFound, "There is no such community.");

    // ── The slug ────────────────────────────────────────────────────────────────────────────

    public static readonly Error SlugRequired = Error.Validation(
        CommunityErrorCodes.SlugRequired, "A community needs a handle.");

    public static readonly Error SlugLength = Error.Validation(
        CommunityErrorCodes.SlugLength,
        "A handle is between 3 and 64 characters.");

    public static readonly Error SlugShape = Error.Validation(
        CommunityErrorCodes.SlugShape,
        "A handle uses lower-case letters, numbers and single hyphens.");

    public static readonly Error SlugReserved = Error.Validation(
        CommunityErrorCodes.SlugReserved,
        "That handle is reserved. Choose another.");

    public static readonly Error SlugNotDerivable = Error.Validation(
        CommunityErrorCodes.SlugNotDerivable,
        "A handle could not be made from that name. Choose one yourself.");

    public static readonly Error SlugTaken = Error.Conflict(
        CommunityErrorCodes.SlugTaken, "That handle is already in use.");

    // ── Identity ────────────────────────────────────────────────────────────────────────────

    public static readonly Error NameRequired = Error.Validation(
        CommunityErrorCodes.NameRequired, "A community needs a name.");

    public static readonly Error NameLength = Error.Validation(
        CommunityErrorCodes.NameLength, "A name is between 2 and 120 characters.");

    /// <summary>
    /// A national or regional community with no country is not a claim the platform can serve: the
    /// directory filters on origin, and a row that cannot answer that filter is invisible in the one
    /// place people look.
    /// </summary>
    public static readonly Error OriginRequired = Error.Validation(
        CommunityErrorCodes.OriginRequired,
        "A national or regional community needs a country of origin.");

    public static readonly Error CountryCodeShape = Error.Validation(
        CommunityErrorCodes.CountryCodeShape,
        "A country is a two-letter ISO 3166-1 code.");

    // ── Lifecycle ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// An ordering the directory does not serve. Each of the three is refused rather than quietly
    /// substituted, and each carries its own reason because the client renders three different
    /// things: an unknown word is a bug, and the other two are honest limits.
    /// </summary>
    public static readonly Error SortUnknown = Error.Validation(
        CommunityErrorCodes.SortUnknown,
        "That is not an ordering the directory offers.");

    /// <summary>
    /// §31: do not imply a popularity the backend does not authoritatively report. The member count
    /// is maintained as members come and go rather than measured, so an order built on it would
    /// present a running total as a ranking.
    /// </summary>
    public static readonly Error SortByMembersUnavailable = Error.Validation(
        CommunityErrorCodes.SortByMembersUnavailable,
        "Communities cannot be ordered by size. The platform does not publish an authoritative member count.");

    /// <summary>
    /// The module pages on a timestamp-keyed cursor. Serving a name order would need a second
    /// boundary scheme; paging in timestamp order under a name label would be the silent fallback
    /// this refusal exists to avoid.
    /// </summary>
    public static readonly Error SortByNameUnavailable = Error.Validation(
        CommunityErrorCodes.SortByNameUnavailable,
        "Communities cannot be ordered by name yet.");

    public static readonly Error NotJoinable = Error.Conflict(
        CommunityErrorCodes.NotJoinable,
        "This community is not open to join at the moment.");

    public static readonly Error AlreadyActive = Error.Conflict(
        CommunityErrorCodes.AlreadyActive, "This community is already open.");

    public static readonly Error ArchivedIsFinal = Error.Conflict(
        CommunityErrorCodes.ArchivedIsFinal,
        "An archived community cannot be reopened.");

    public static readonly Error SuspensionNeedsReason = Error.Validation(
        CommunityErrorCodes.SuspensionNeedsReason,
        "Say why the community is being suspended. Members are shown this.");

    // ── Membership ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A visibility the platform does not have. Refused rather than defaulted, in either
    /// direction: defaulting to Private ignores somebody opting in, and defaulting to Public
    /// discloses a membership on a typo.
    /// </summary>
    public static readonly Error UnknownMembershipVisibility = Error.Validation(
        CommunityErrorCodes.UnknownMembershipVisibility,
        "That is not a visibility setting.");

    /// <summary>
    /// An act the membership route does not offer. Refused rather than treated as a join, because
    /// guessing at an unrecognised verb is how a client that meant "follow" ends up a member.
    /// </summary>
    public static readonly Error UnknownMembershipAction = Error.Validation(
        CommunityErrorCodes.UnknownMembershipAction,
        "That is not something you can do to a membership.");

    public static readonly Error AlreadyMember = Error.Conflict(
        CommunityErrorCodes.AlreadyMember, "You already belong to this community.");

    public static readonly Error NotAMember = Error.Conflict(
        CommunityErrorCodes.NotAMember, "You do not belong to this community.");

    public static readonly Error MembershipRevoked = Error.Forbidden(
        CommunityErrorCodes.MembershipRevoked,
        "Your membership of this community was removed.");

    public static readonly Error InviteRequired = Error.Forbidden(
        CommunityErrorCodes.InviteRequired,
        "This community is joined by invitation only.");

    /// <summary>
    /// Clearing a primary that was never set is not an error worth raising — but setting one to a
    /// community the caller does not belong to is, because it would put a community on their profile
    /// that they have no standing in.
    /// </summary>
    public static readonly Error PrimaryRequiresMembership = Error.Conflict(
        CommunityErrorCodes.PrimaryRequiresMembership,
        "You can only make a community your primary one if you belong to it.");
}
