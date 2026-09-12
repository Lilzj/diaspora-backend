using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Events;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Domain.Communities;

/// <summary>
/// A community: a group of people with a shared origin, language, faith, city or interest.
/// <para>
/// <b>This is the entity the whole multi-community register was waiting on.</b> Twenty-three
/// capabilities — the directory, profiles, joining, a primary community, scoped content, scoped
/// search, moderation, statistics — all assumed a community was a first-class thing with its own
/// identity. Nothing was: <c>GET /communities</c> answered 404, and the client had been built
/// against a simulated adapter. Every one of those capabilities is a read or a write over this
/// aggregate.
/// </para>
///
/// <para>
/// <b>A community is not a country, and the distinction is the point of the phase.</b> The platform
/// already had 192 countries and a <c>CountryRef</c> on every publisher. A country is a fact about
/// a place; a community is a group of people who may be spread across forty of them. "Nigerians in
/// London" is one community, "Nigerians" is another, and the second is not the union of the first
/// — a member belongs to what they say they belong to.
/// </para>
///
/// <para>
/// <b>A community is not an organization either.</b> <see cref="Memberships.MembershipProgramme"/>
/// is what an organization sells; this is what a person belongs to. An organization can serve a
/// community without being one, and a community exists whether or not any organization serves it.
/// </para>
///
/// <para>
/// <b>The identifier and the slug are both stable, and neither is the name.</b> The register asked
/// for "a stable identifier independent of country code" and "a slug scheme stable enough to put in
/// a deep link". A rename changes <see cref="Name"/> and nothing else — the slug somebody put in a
/// message last year keeps resolving. See <see cref="CommunitySlug"/>.
/// </para>
///
/// <para>
/// <b>Member count is not stored as a running total anybody may set.</b> It is maintained by the
/// aggregate as members join and leave, and §31 forbids sorting a directory by a count the backend
/// does not authoritatively report — so a deployment that cannot vouch for the number must publish
/// nothing rather than an estimate. <see cref="MemberCount"/> is that number, and it is only ever
/// moved by <see cref="RecordMemberJoined"/> and <see cref="RecordMemberLeft"/>.
/// </para>
/// </summary>
public sealed class Community : AggregateRoot<CommunityId>, IAuditableEntity
{
    public const int NameMinimumLength = 2;
    public const int NameMaximumLength = 120;
    public const int TaglineMaximumLength = 200;
    public const int DescriptionMaximumLength = 4000;

    private readonly List<string> _presenceCountryCodes = [];
    private readonly List<string> _supportedLanguages = [];

    private Community(
        CommunityId id,
        CommunitySlug slug,
        string name,
        CommunityType type,
        CommunityVisibility visibility,
        string? originCountryCode,
        string? originRegionName)
        : base(id)
    {
        Slug = slug;
        Name = name;
        Type = type;
        Visibility = visibility;
        OriginCountryCode = originCountryCode;
        OriginRegionName = originRegionName;
        Status = CommunityStatus.Pending;
    }

    // EF Core.
    private Community() { }

    /// <summary>The handle. Issued once and never changed. See <see cref="CommunitySlug"/>.</summary>
    public CommunitySlug Slug { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public string? Tagline { get; private set; }

    public string? Description { get; private set; }

    public CommunityType Type { get; private set; }

    public CommunityStatus Status { get; private set; }

    public CommunityVisibility Visibility { get; private set; }

    /// <summary>ISO 3166-1 alpha-2, upper case. Required for a national or regional community.</summary>
    public string? OriginCountryCode { get; private set; }

    /// <summary>For a region that is not a country — "West Africa", "the Caribbean".</summary>
    public string? OriginRegionName { get; private set; }

    public string? PrimaryLanguage { get; private set; }

    // ── Identity ────────────────────────────────────────────────────────────────────────────
    //
    // Three fields a person uses to recognise their community at a glance, and the reason they are
    // on the aggregate rather than left null on the wire: the client's card falls back from flag to
    // icon to bare name, and a RegionalDiaspora or InterestCommunity has no country, so no flag.
    // Without an icon those communities render as grey text — which is the type this whole phase
    // exists to make first-class. Serving a contract-valid response that makes the screen worse
    // than the adapter it replaces is not closing the gap.
    //
    // Advisory, all three. Nothing is gated on them and none is required.

    /// <summary>The community's mark. A media id resolved to a URL at the edge, never a raw link.</summary>
    public Guid? LogoMediaId { get; private set; }

    /// <summary>An Ionicons name. What a community with no flag is recognised by.</summary>
    public string? IconName { get; private set; }

    /// <summary>Hex, <c>#rrggbb</c>. Advisory: contrast is the client's to guarantee, not this
    /// module's to promise.</summary>
    public string? AccentColor { get; private set; }

    /// <summary>Where this community has a presence, which is not where it is from.</summary>
    public IReadOnlyCollection<string> PresenceCountryCodes => _presenceCountryCodes.AsReadOnly();

    public IReadOnlyCollection<string> SupportedLanguages => _supportedLanguages.AsReadOnly();

    /// <summary>
    /// Members in good standing. Moved only by this aggregate; never assigned from outside.
    /// </summary>
    public int MemberCount { get; private set; }

    /// <summary>Why the community is suspended or archived. Shown to members verbatim.</summary>
    public string? StatusReason { get; private set; }

    /// <summary>
    /// Whether joining goes to a queue.
    /// <para>
    /// <b>The documented rule the register asked for: it is a property of the community, set by the
    /// people who run it, and false unless they say otherwise.</b> Not derived from visibility, which
    /// answers a different question — Unlisted is about who can find the community, InviteOnly about
    /// who may ask at all, and this about what happens when they do. Deriving one from another would
    /// mean an administrator could not open a private community to anyone who found the link, or
    /// close a public one to review.
    /// </para>
    /// <para>
    /// The client renders this before the button is pressed, so somebody is told they are asking
    /// rather than joining. That is the whole reason it crosses the wire.
    /// </para>
    /// </summary>
    public bool RequiresApproval { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    /// <summary>Whether somebody may join right now. Read by the join path and by capabilities.</summary>
    public bool IsJoinable => Status == CommunityStatus.Active;

    /// <summary>Whether this community may appear in directory results at all.</summary>
    public bool IsListable =>
        Visibility == CommunityVisibility.Public
        && Status is CommunityStatus.Active or CommunityStatus.Suspended;

    // ── Creation ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Proposes a community. It opens in <see cref="CommunityStatus.Pending"/> and is not joinable
    /// until <see cref="Open"/> is called — a community that appeared in the directory the instant
    /// somebody typed a name would make the directory a scratchpad.
    /// </summary>
    public static Result<Community> Propose(
        CommunityId id,
        CommunitySlug slug,
        string? name,
        CommunityType type,
        CommunityVisibility visibility,
        string? originCountryCode = null,
        string? originRegionName = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(slug);

        var trimmedName = name?.Trim();

        if (string.IsNullOrEmpty(trimmedName))
        {
            return Result.Failure<Community>(CommunityErrors.NameRequired);
        }

        if (trimmedName.Length is < NameMinimumLength or > NameMaximumLength)
        {
            return Result.Failure<Community>(CommunityErrors.NameLength);
        }

        var country = NormaliseCountryCode(originCountryCode);

        if (originCountryCode is not null && country is null)
        {
            return Result.Failure<Community>(CommunityErrors.CountryCodeShape);
        }

        // A national or regional community that cannot answer "where from" is invisible to the one
        // filter the directory is built around.
        if (type is CommunityType.NationalDiaspora or CommunityType.RegionalDiaspora
            && country is null
            && string.IsNullOrWhiteSpace(originRegionName))
        {
            return Result.Failure<Community>(CommunityErrors.OriginRequired);
        }

        return Result.Success(new Community(
            id, slug, trimmedName, type, visibility, country, originRegionName?.Trim()));
    }

    // ── Lifecycle ───────────────────────────────────────────────────────────────────────────

    /// <summary>Opens a pending or suspended community. Archived is final and refuses.</summary>
    public Result Open()
    {
        if (Status == CommunityStatus.Archived)
        {
            return Result.Failure(CommunityErrors.ArchivedIsFinal);
        }

        if (Status == CommunityStatus.Active)
        {
            return Result.Failure(CommunityErrors.AlreadyActive);
        }

        Status = CommunityStatus.Active;
        StatusReason = null;
        return Result.Success();
    }

    /// <summary>
    /// Stops a community without ending it. Members keep their standing and are shown
    /// <paramref name="reason"/> verbatim, which is why it is required.
    /// </summary>
    public Result Suspend(string? reason)
    {
        if (Status == CommunityStatus.Archived)
        {
            return Result.Failure(CommunityErrors.ArchivedIsFinal);
        }

        var trimmed = reason?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return Result.Failure(CommunityErrors.SuspensionNeedsReason);
        }

        Status = CommunityStatus.Suspended;
        StatusReason = trimmed;
        return Result.Success();
    }

    /// <summary>
    /// Ends a community. Irreversible, and it stays readable: the links that pointed here resolve
    /// to an explanation rather than a 404.
    /// </summary>
    public Result Archive(string? reason)
    {
        if (Status == CommunityStatus.Archived)
        {
            return Result.Success();
        }

        Status = CommunityStatus.Archived;
        StatusReason = reason?.Trim();
        return Result.Success();
    }

    /// <summary>
    /// Closes a community at the end of an approved deletion request — 11 Sep 2026.
    /// <para>
    /// <b>Closed, not erased.</b> A community is never hard-deleted here: everything written in it
    /// belongs to the people who wrote it, and the links that point at it resolve to an explanation
    /// rather than a 404. What ends is the community as a going concern — nobody joins, nobody posts,
    /// and it leaves the directory.
    /// </para>
    /// <para>
    /// Separate from <see cref="Archive"/>, which is silent and idempotent, because this one has to
    /// be heard: the owner who asked for it is told it happened. Archiving twice must not announce a
    /// closure twice, so an already-archived community is refused rather than waved through.
    /// </para>
    /// </summary>
    public Result CloseForDeletion(UserId closedFor, string? reason)
    {
        ArgumentNullException.ThrowIfNull(closedFor);

        if (Status == CommunityStatus.Archived)
        {
            return Result.Failure(CommunityErrors.ArchivedIsFinal);
        }

        Status = CommunityStatus.Archived;
        StatusReason = reason?.Trim();

        RaiseDomainEvent(new CommunityDeletedDomainEvent
        {
            CommunityId = Id,
            CommunityName = Name,
            ClosedFor = closedFor,
            Reason = StatusReason
        });

        return Result.Success();
    }

    // ── Description ─────────────────────────────────────────────────────────────────────────

    /// <summary>Renames the community. The slug is untouched — see the type summary.</summary>
    public Result Rename(string? name)
    {
        var trimmed = name?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return Result.Failure(CommunityErrors.NameRequired);
        }

        if (trimmed.Length is < NameMinimumLength or > NameMaximumLength)
        {
            return Result.Failure(CommunityErrors.NameLength);
        }

        Name = trimmed;
        return Result.Success();
    }

    public void Describe(string? tagline, string? description)
    {
        Tagline = Truncate(tagline, TaglineMaximumLength);
        Description = Truncate(description, DescriptionMaximumLength);
    }

    public void SetVisibility(CommunityVisibility visibility) => Visibility = visibility;

    /// <summary>Sets whether joining is a request rather than an act. See <see cref="RequiresApproval"/>.</summary>
    public void SetJoinPolicy(bool requiresApproval) => RequiresApproval = requiresApproval;

    /// <summary>
    /// Sets the community's mark. Each argument is applied as given, <c>null</c> included, so
    /// clearing a logo is one call rather than a second method.
    /// <para>
    /// A malformed accent colour is dropped rather than refused. It is advisory display metadata —
    /// refusing the whole edit because a hex string lost its hash would block a name change on a
    /// field nothing depends on.
    /// </para>
    /// </summary>
    public void SetIdentity(Guid? logoMediaId, string? iconName, string? accentColor)
    {
        LogoMediaId = logoMediaId;

        var icon = iconName?.Trim();
        IconName = string.IsNullOrEmpty(icon) ? null : icon;

        var colour = accentColor?.Trim();
        AccentColor = IsHexColour(colour) ? colour!.ToLowerInvariant() : null;
    }

    /// <summary><c>#rrggbb</c>, and nothing else. Written out rather than a regex because the
    /// aggregate would have to become partial to carry a <c>[GeneratedRegex]</c>, and this is
    /// clearer than the regex would have been.</summary>
    private static bool IsHexColour(string? value)
    {
        if (value is not { Length: 7 } || value[0] != '#')
        {
            return false;
        }

        for (var index = 1; index < 7; index++)
        {
            if (!Uri.IsHexDigit(value[index]))
            {
                return false;
            }
        }

        return true;
    }

    public void SetLanguages(string? primaryLanguage, IEnumerable<string>? supported)
    {
        PrimaryLanguage = string.IsNullOrWhiteSpace(primaryLanguage) ? null : primaryLanguage.Trim();

        _supportedLanguages.Clear();
        foreach (var language in Distinct(supported))
        {
            _supportedLanguages.Add(language);
        }
    }

    /// <summary>
    /// Records where this community has a presence. Distinct from where it is from: a Nigerian
    /// community present in the UK, Canada and the UAE has one origin and three presences.
    /// </summary>
    public Result SetPresence(IEnumerable<string>? countryCodes)
    {
        var normalised = new List<string>();

        foreach (var candidate in Distinct(countryCodes))
        {
            var code = NormaliseCountryCode(candidate);

            if (code is null)
            {
                return Result.Failure(CommunityErrors.CountryCodeShape);
            }

            if (!normalised.Contains(code, StringComparer.Ordinal))
            {
                normalised.Add(code);
            }
        }

        _presenceCountryCodes.Clear();
        _presenceCountryCodes.AddRange(normalised);
        return Result.Success();
    }

    // ── Membership counting ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// The sizes worth marking. A milestone is reached the first time the count arrives at one.
    /// </summary>
    public static readonly IReadOnlyList<int> MemberMilestones =
        [10, 25, 50, 100, 250, 500, 1_000, 2_500, 5_000, 10_000, 25_000, 50_000, 100_000];

    /// <summary>
    /// The largest milestone this community has ever reached. Zero before the first.
    /// <para>
    /// <b>Stored, so a milestone is reached once.</b> A community that goes from 100 to 99 when
    /// somebody leaves and back to 100 when somebody joins has not reached 100 members twice, and a
    /// notice that said so would teach its administrators to ignore the next one. Only ever rises.
    /// </para>
    /// </summary>
    public int HighestMemberMilestone { get; private set; }

    /// <summary>
    /// Called by the membership path when somebody joins. Not public API: a count anybody could set
    /// is a count nobody can trust, and §31 forbids sorting the directory by an unreliable one.
    /// <para>
    /// Raises <see cref="CommunityMilestoneReachedDomainEvent"/> when the count arrives at a
    /// milestone it has never reached — the event behind <c>community.milestoneReached</c>, and the
    /// last of the four the Backend Gap Build Order called genuinely missing.
    /// </para>
    /// </summary>
    public void RecordMemberJoined()
    {
        MemberCount++;

        var reached = MemberMilestones.LastOrDefault(milestone => milestone <= MemberCount);

        if (reached <= HighestMemberMilestone)
        {
            return;
        }

        HighestMemberMilestone = reached;

        RaiseDomainEvent(new CommunityMilestoneReachedDomainEvent
        {
            CommunityId = Id,
            CommunityName = Name,
            Milestone = reached,
            MemberCount = MemberCount
        });
    }

    /// <summary>
    /// Called when somebody leaves or is removed. Floors at zero rather than throwing: a negative
    /// member count is a reporting bug, and taking down a leave request over it helps nobody.
    /// </summary>
    public void RecordMemberLeft() => MemberCount = Math.Max(0, MemberCount - 1);

    // ── Audit ───────────────────────────────────────────────────────────────────────────────

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Upper-cases and validates an ISO 3166-1 alpha-2 code. Null in, null out.</summary>
    internal static string? NormaliseCountryCode(string? value)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length != 2 || !trimmed.All(char.IsLetter))
        {
            return null;
        }

        return trimmed.ToUpperInvariant();
    }

    private static string? Truncate(string? value, int limit)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= limit ? trimmed : trimmed[..limit];
    }

    private static IEnumerable<string> Distinct(IEnumerable<string>? values) =>
        (values ?? [])
            .Select(value => value?.Trim())
            .Where(value => !string.IsNullOrEmpty(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase);
}
