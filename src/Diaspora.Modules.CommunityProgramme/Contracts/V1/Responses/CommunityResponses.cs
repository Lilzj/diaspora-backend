namespace Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// The community's wire contracts — Frontend Phase 13, GAP-001 and GAP-002.
//
// Shaped to `src/types/community-directory.ts` field for field, because the client has been built
// against a development adapter for the whole of Phase 13 and the migration is meant to be an
// import swap. Where the register's prose and the client's TypeScript disagree, the TypeScript
// wins: it is what the screens actually destructure.
//
// One place they disagree and the register loses: the register sketches
// `{ data: [...], meta: { nextCursor, hasMore } }`, and this module already has
// `CommunityPageResponse<T>` carrying paging inside `data`. The client's `CommunityDirectoryPage`
// is `{ items, nextCursor, hasMore, totalCount }` — the existing envelope, not the sketch. Two
// paging shapes in one module would be a worse outcome than a prose sketch going unmatched.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// A value and where it came from.
/// <para>
/// <b>The client's <c>Sourced&lt;T&gt;</c>, and it is not decoration.</b> §29 forbids inventing
/// authoritative metrics and §31 forbids implying a popularity the backend does not report. A screen
/// holding a member count cannot print it without also holding this marker, which is what stopped
/// the simulated adapter's counts being read as platform facts.
/// </para>
/// <para>
/// Every value this module puts in one is <c>"backend"</c>. The field exists so the client's
/// rendering path is the same whichever arm answered — not so this module can hedge.
/// </para>
/// </summary>
public sealed record SourcedResponse<T>
{
    public required T Value { get; init; }

    /// <summary><c>backend</c> or <c>simulated</c>. Always the former here.</summary>
    public required string Source { get; init; }

    public static SourcedResponse<T> Backed(T value) => new() { Value = value, Source = "backend" };
}

/// <summary>
/// Where a community is from, as properties rather than identity.
/// <para>
/// <b>Every field is optional and that is the point of the phase.</b> §2: community logic is not
/// country logic. A <c>RegionalDiaspora</c> has no country code, an <c>InterestCommunity</c> has no
/// geography at all, and neither is malformed.
/// </para>
/// </summary>
public sealed record CommunityOriginResponse
{
    /// <summary>ISO 3166-1 alpha-2. Never the community's identifier.</summary>
    public string? CountryCode { get; init; }

    /// <summary>
    /// Left null by this module rather than looked up.
    /// <para>
    /// The client already carries all 192 countries with their names and flags in
    /// <c>mocks/countries.ts</c> and renders from the code. Serving a name here would be a second
    /// source of truth for a string the device already has, in one language, with no way to
    /// localise it — §58 forbids exactly that.
    /// </para>
    /// </summary>
    public string? CountryName { get; init; }

    /// <summary>Null, for the same reason as <see cref="CountryName"/>. The client has the flag.</summary>
    public string? Flag { get; init; }

    /// <summary>For a community that spans countries. "West Africa", "the Caribbean".</summary>
    public string? RegionName { get; init; }
}

/// <summary>
/// What a community is recognised by.
/// <para>
/// The client's card falls back flag → icon → bare name. A community with no country has no flag,
/// so <see cref="IconName"/> is the only thing between it and grey text.
/// </para>
/// </summary>
public sealed record CommunityIdentityResponse
{
    /// <summary>
    /// The community's mark, as a media id.
    /// <para>
    /// <b>An id and not a URL, against the client's <c>logoUrl</c>, and deliberately.</b> Every
    /// module on this platform answers <c>logoMediaId</c> — B12's <c>OrganizationResponse</c> does,
    /// and this module's own client file already says so in as many words: "Logos are media
    /// identifiers, not URLs. B18 answers <c>logoMediaId</c>". The device resolves them through
    /// <c>mediaUri()</c>, which is where the signing and the sizing live.
    /// </para>
    /// <para>
    /// Serving a URL here would make this the one module whose logos bypass that, and would give
    /// the module a dependency on Media's URL factory for a field nothing is gated on. The client's
    /// certified arm maps this to <c>logoUrl</c> in one line, where every other logo is already
    /// mapped.
    /// </para>
    /// </summary>
    public Guid? LogoMediaId { get; init; }

    /// <summary>An Ionicons name.</summary>
    public string? IconName { get; init; }

    /// <summary>Hex. Advisory — the client owns contrast.</summary>
    public string? AccentColor { get; init; }
}

/// <summary>
/// A community, in full.
/// <para>
/// <b>There is no <c>CommunityRef</c> on the wire.</b> The client's <c>Community</c> extends
/// <c>CommunityRef</c>, so one flat record satisfies both and a caller that wants the compact shape
/// reads the six fields it needs. Two records would be two things to keep in step.
/// </para>
/// </summary>
public sealed record CommunityResponse
{
    public required Guid Id { get; init; }

    /// <summary>The handle. Stable for the community's life — a deep link sent last year resolves.</summary>
    public required string Slug { get; init; }

    public required string Name { get; init; }

    /// <summary>One of the six <c>CommunityType</c> names, not its number.</summary>
    public required string Type { get; init; }

    public required CommunityOriginResponse Origin { get; init; }

    public required CommunityIdentityResponse Identity { get; init; }

    public string? Tagline { get; init; }

    public string? Description { get; init; }

    /// <summary>
    /// One of <c>Pending | Active | Suspended | Archived</c>. Never <c>Unavailable</c> — that is the
    /// client's word for "the server would not say", and a server does not say it about itself.
    /// </summary>
    public required string Status { get; init; }

    public required string Visibility { get; init; }

    public string? PrimaryLanguage { get; init; }

    public IReadOnlyList<string> SupportedLanguages { get; init; } = [];

    /// <summary>
    /// Members in good standing, with provenance.
    /// <para>
    /// <b>Null rather than zero where it is withheld.</b> A private community the caller does not
    /// belong to reports no size at all — zero would be a statement, and a false one.
    /// </para>
    /// </summary>
    public SourcedResponse<int>? MemberCount { get; init; }

    /// <summary>Where the community has a presence, which is not where it is from.</summary>
    public IReadOnlyList<string> PresenceCountryCodes { get; init; } = [];

    public DateTimeOffset? CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>
    /// Why a suspended or archived community is in that state. Rendered verbatim.
    /// <para>
    /// The register asked for this specifically: a member opening a deep link to a community that
    /// stopped is entitled to the reason rather than a generic error (§74, §75).
    /// </para>
    /// </summary>
    public string? StatusReason { get; init; }
}

/// <summary>
/// One row of the directory.
/// <para>
/// <b>The membership state is the server's, and that is the whole reason this record exists rather
/// than a bare list of communities.</b> A client deriving "you are a member" from a cached list
/// offers Leave to somebody whose membership was revoked an hour ago.
/// </para>
/// </summary>
public sealed record CommunityDirectoryEntryResponse
{
    public required CommunityResponse Community { get; init; }

    /// <summary>
    /// One of the ten <c>CommunityMembershipState</c> names. <c>None</c> for a caller with no row,
    /// and for every anonymous caller.
    /// </summary>
    public required string MembershipState { get; init; }

    public required bool IsPrimary { get; init; }

    /// <summary>
    /// Why this community is in the list, where the server has something true to say. Null on a
    /// plain browse — a reason invented per row would be a ranking claim nobody made.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// What one caller may do in one community. Eleven booleans and the roles, for display.
/// <para>
/// Named <c>CommunityMember…</c> rather than <c>CommunityCapabilities…</c> because B6 already has a
/// record by that name serving something else entirely — its social feature catalogue. Two records
/// with one name in one codebase is a review hazard for no gain; the wire field names are what the
/// client reads, and they are unchanged.
/// </para>
/// </summary>
public sealed record CommunityMemberCapabilitiesResponse
{
    public required Guid CommunityId { get; init; }

    /// <summary>Echoed for display. Not the source of any decision, here or on the device.</summary>
    public required IReadOnlyList<string> Roles { get; init; }

    public required bool CanViewMembers { get; init; }

    public required bool CanViewPrivateStatistics { get; init; }

    public required bool CanPostToCommunity { get; init; }

    public required bool CanModerateContent { get; init; }

    public required bool CanReviewReports { get; init; }

    public required bool CanPublishNotice { get; init; }

    public required bool CanManageRules { get; init; }

    public required bool CanManageRoles { get; init; }

    /// <summary>Appoint or remove administrators. The Owner's alone.</summary>
    public required bool CanAppointAdministrators { get; init; }

    /// <summary>Leave the community. Every current member except its Owner.</summary>
    public required bool CanLeave { get; init; }

    /// <summary>Ask Diaspora to close this community. The Owner's alone.</summary>
    public required bool CanRequestDeletion { get; init; }

    /// <summary>Offer the community to one of its moderators or administrators. The Owner's alone.</summary>
    public required bool CanTransferOwnership { get; init; }

    public required bool CanViewModerationQueue { get; init; }

    public required bool CanViewAmbassadorDirectory { get; init; }

    public required bool CanManageInitiatives { get; init; }
}

/// <summary>
/// One person's standing in one community, as the client models it.
/// <para>
/// <b>The capability set rides along rather than being fetched separately.</b> Every screen that
/// renders a membership also needs to know what the member may do there, and a second round trip per
/// row would make the membership list N+1 on the device instead of on the server.
/// </para>
/// </summary>
public sealed record CommunityMembershipResponse
{
    public required Guid CommunityId { get; init; }

    /// <summary>One of the ten <c>CommunityMembershipState</c> names.</summary>
    public required string State { get; init; }

    /// <summary>At most one membership in a set carries this. Enforced by a filtered unique index.</summary>
    public required bool IsPrimary { get; init; }

    /// <summary>Display only. See <see cref="CommunityMemberCapabilitiesResponse"/>.</summary>
    public required IReadOnlyList<string> Roles { get; init; }

    public required CommunityMemberCapabilitiesResponse Capabilities { get; init; }

    /// <summary><c>Private</c> unless the member chose otherwise. Joining publishes nothing (§41).</summary>
    public required string Visibility { get; init; }

    public DateTimeOffset? JoinedAt { get; init; }

    public DateTimeOffset? VerifiedAt { get; init; }

    /// <summary>
    /// Why the community decided what it decided, on <c>Declined</c> and <c>Revoked</c>. Rendered
    /// verbatim, and null on <c>Left</c> — nobody decided that but the member.
    /// </summary>
    public string? StateReason { get; init; }

    /// <summary>
    /// Whether joining this community goes to a queue, so the button can say so before it is
    /// pressed rather than after.
    /// </summary>
    public bool? RequiresApproval { get; init; }
}

/// <summary>
/// The caller's whole set of community relationships.
/// <para>
/// <b>One object rather than a list plus a separate primary, so the two cannot disagree.</b> The
/// client says why: a set with two primaries is a server bug that should render as one primary
/// rather than as an arbitrary pick, so it reads <see cref="PrimaryCommunityId"/> and does not scan.
/// </para>
/// </summary>
public sealed record CommunityMembershipSetResponse
{
    public required IReadOnlyList<CommunityMembershipResponse> Memberships { get; init; }

    /// <summary>The server's own statement of which is primary. Null when there is none.</summary>
    public Guid? PrimaryCommunityId { get; init; }

    /// <summary>When this was read. Drives the client's "showing information from…" line (§45).</summary>
    public required DateTimeOffset FetchedAt { get; init; }
}

/// <summary>
/// One member of a community, as another member sees them.
/// <para>
/// <b>Only members who chose to be visible appear</b>, and the caller themselves. §41 makes belonging
/// private by default, and a member list that showed everybody to every member would publish exactly
/// what that rule keeps quiet. A role can only be held with a visible membership, so everybody who
/// holds one is on this list.
/// </para>
/// </summary>
public sealed record CommunityMemberResponse
{
    public required Guid UserId { get; init; }

    /// <summary>Null only when the identity store could not answer, never invented.</summary>
    public string? DisplayName { get; init; }

    /// <summary><c>Owner</c>, <c>Administrator</c>, <c>Moderator</c>, <c>Ambassador</c>. Empty for a member.</summary>
    public required IReadOnlyList<string> Roles { get; init; }

    public required string Visibility { get; init; }

    public DateTimeOffset? JoinedAt { get; init; }

    /// <summary>Whether this row is the caller's own.</summary>
    public required bool IsYou { get; init; }
}

// ── Closing a community, and handing one over — 11 Sep 2026 ─────────────────────────────────

/// <summary>
/// A request to close a community, as its owner and the administrators reviewing it see it.
/// <para>
/// The names ride along because every screen that shows one of these shows who asked, and a client
/// that had to resolve a user id would either make a second request or print a Guid.
/// </para>
/// </summary>
public sealed record CommunityDeletionRequestResponse
{
    public required Guid Id { get; init; }

    public required Guid CommunityId { get; init; }

    public required string CommunityName { get; init; }

    public required string CommunitySlug { get; init; }

    /// <summary><c>AwaitingReview | Approved | Rejected | Withdrawn | Completed</c>.</summary>
    public required string Status { get; init; }

    /// <summary>The owner's words.</summary>
    public required string Reason { get; init; }

    public required Guid RequestedBy { get; init; }

    public string? RequestedByName { get; init; }

    public required DateTimeOffset RequestedAt { get; init; }

    /// <summary>The earliest the community can close. Thirty days after it was asked for.</summary>
    public required DateTimeOffset EffectiveAt { get; init; }

    public DateTimeOffset? ReviewedAt { get; init; }

    /// <summary>The administrator's words, shown to the owner verbatim.</summary>
    public string? DecisionReason { get; init; }
}

/// <summary>An offer to hand a community over, as either side sees it.</summary>
public sealed record CommunityOwnershipOfferResponse
{
    public required Guid Id { get; init; }

    public required Guid CommunityId { get; init; }

    public required string CommunityName { get; init; }

    /// <summary>So a notification can link to the community by handle.</summary>
    public required string CommunitySlug { get; init; }

    /// <summary><c>Offered | Accepted | Declined | Withdrawn | Expired</c>.</summary>
    public required string Status { get; init; }

    public required Guid OfferedBy { get; init; }

    public string? OfferedByName { get; init; }

    public required Guid OfferedTo { get; init; }

    public string? OfferedToName { get; init; }

    public string? Message { get; init; }

    public required DateTimeOffset OfferedAt { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public string? DeclineReason { get; init; }
}
