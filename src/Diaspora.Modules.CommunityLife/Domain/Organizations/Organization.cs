using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Events;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Domain.Organizations;

/// <summary>
/// A body of people that persists between the things it does.
/// <para>
/// <b>The aggregate exists because a church is not a service.</b> An organization outlives every
/// event it runs, has members who are not attendees, is verified as an institution rather than as
/// an occasion, and answers questions nobody asks of an event — when is it open, what languages
/// will I be understood in, who leads it, how do I join. Modelling it as a field on an event
/// collapses all of that, and modelling an event as a child of it makes the many events that have
/// no organization behind them unrepresentable.
/// </para>
/// <para>
/// <b>What it owns:</b> its own profile, its leaders, its operating hours, its languages, its
/// services, its media references, its guide references and its moderation trail. <b>What it points
/// at:</b> an organization type by catalogue key, a B4 administrative area, B5 media identifiers,
/// B7 providers and B8 guides, and a Trust decision it can only ever receive.
/// </para>
/// <para>
/// <b>Members are not here.</b> An organization with eleven thousand members would load eleven
/// thousand rows to render its name, and an administrator approving one request would take a write
/// lock on the whole institution. Membership is its own aggregate keyed by organization, and the
/// counts on <see cref="Statistics"/> are settled from exactly one place — the membership status
/// handler — which is what makes them reconcilable.
/// </para>
/// <para>
/// <b>Concurrency is optimistic and explicit through <see cref="Revision"/>.</b> An editor sends
/// back the revision it loaded and <see cref="EnsureRevision"/> rejects a stale save. Two committee
/// members genuinely do edit one organization page from two desks, which is exactly the case B11
/// found the hard way when it shipped without declaring the token.
/// </para>
/// </summary>
public sealed class Organization
    : AggregateRoot<OrganizationId>, IAuditableEntity, ISoftDeletable
{
    private readonly List<OrganizationLeader> _leaders = [];
    private readonly List<OrganizationOperatingHour> _operatingHours = [];
    private readonly List<OrganizationLanguage> _languages = [];
    private readonly List<OrganizationService> _services = [];
    private readonly List<OrganizationMediaReference> _mediaReferences = [];
    private readonly List<OrganizationGuideReference> _guideReferences = [];
    private readonly List<OrganizationModerationNote> _moderationNotes = [];

    private Organization(
        OrganizationId id,
        UserId createdByUserId,
        OrganizationName name,
        CommunityLifeSlug slug,
        CommunityLifeDescription description,
        string organizationTypeKey,
        CommunityLifeLocationContext location,
        DateTimeOffset createdAt) : base(id)
    {
        CreatedByUserId = createdByUserId;
        Name = name;
        Slug = slug;
        Description = description;
        OrganizationTypeKey = organizationTypeKey;
        Location = location;
        Contact = ContactDetails.Empty;
        SocialLinks = SocialLinks.None;
        MembershipPolicy = MembershipPolicy.Open;
        Statistics = MembershipStatistics.Empty;
        TrustMark = CommunityLifeTrustMark.Unverified;
        Status = OrganizationStatus.Draft;
        Visibility = OrganizationVisibility.Private;
        ModerationState = CommunityLifeModerationState.Clear;
        CreatedAt = createdAt;
    }

    private Organization() { }

    // ── Ownership & classification ──────────────────────────────────────────────────────────

    /// <summary>
    /// The member who created it. Not the same as the owner, which is a membership role and can be
    /// handed on — an audit trail that recorded only the current owner could not say who set up a
    /// page that turned out to be an impersonation.
    /// </summary>
    public UserId CreatedByUserId { get; private set; } = default!;

    /// <summary>
    /// The organization-type catalogue key. A key rather than an identifier because the catalogue
    /// is configurable — see <see cref="Catalog.OrganizationType"/>.
    /// </summary>
    public string OrganizationTypeKey { get; private set; } = string.Empty;

    /// <summary>
    /// The B7 provider behind it, when there is one. A training company that is also a professional
    /// association is one institution wearing two hats, and this is how the two are joined without
    /// either module owning the other.
    /// </summary>
    public Guid? ProviderId { get; private set; }

    // ── Content ─────────────────────────────────────────────────────────────────────────────

    public OrganizationName Name { get; private set; } = default!;

    public CommunityLifeSlug Slug { get; private set; } = default!;

    public CommunityLifeDescription Description { get; private set; } = default!;

    /// <summary>Why it exists. Optional, and the field most organizations fill in first.</summary>
    public string? Mission { get; private set; }

    /// <summary>Where it is going. Optional, and rarely used outside formal bodies.</summary>
    public string? Vision { get; private set; }

    public CommunityLifeLocationContext Location { get; private set; } = default!;

    public ContactDetails Contact { get; private set; } = default!;

    public SocialLinks SocialLinks { get; private set; } = default!;

    public MembershipPolicy MembershipPolicy { get; private set; } = default!;

    // ── Owned collections ───────────────────────────────────────────────────────────────────
    //
    // Every field below is named for the property in front of it, and that is load-bearing rather
    // than tidy. Each getter hands back a fresh read-only view, so EF cannot write through it and
    // must find the backing field instead — which it does by a naming convention (`_camelCase` of
    // the property) and by nothing else. B11 shipped `_media` behind `MediaReferences`, which
    // compiled, drafted, published and saved perfectly well, and threw "Collection is read-only"
    // the first time a row with one was read back.

    public IReadOnlyList<OrganizationLeader> Leaders =>
        _leaders.OrderBy(leader => leader.Order).ToList().AsReadOnly();

    public IReadOnlyList<OrganizationOperatingHour> OperatingHours =>
        _operatingHours.OrderBy(hour => hour.Day).ToList().AsReadOnly();

    public IReadOnlyList<OrganizationLanguage> Languages =>
        _languages.OrderBy(language => language.Order).ToList().AsReadOnly();

    public IReadOnlyList<OrganizationService> Services =>
        _services.OrderBy(service => service.Order).ToList().AsReadOnly();

    public IReadOnlyList<OrganizationMediaReference> MediaReferences =>
        _mediaReferences.AsReadOnly();

    public IReadOnlyList<OrganizationGuideReference> GuideReferences =>
        _guideReferences.AsReadOnly();

    public IReadOnlyList<OrganizationModerationNote> ModerationNotes =>
        _moderationNotes.AsReadOnly();

    // ── State ───────────────────────────────────────────────────────────────────────────────

    public OrganizationStatus Status { get; private set; }

    public OrganizationVisibility Visibility { get; private set; }

    public CommunityLifeModerationState ModerationState { get; private set; }

    public CommunityLifeTrustMark TrustMark { get; private set; } = default!;

    public MembershipStatistics Statistics { get; private set; } = default!;

    public bool IsFeatured { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>The first publication, which never moves. What directory growth is measured from.</summary>
    public DateTimeOffset? FirstPublishedAt { get; private set; }

    /// <summary>
    /// The last time anything happened under it — an event published, a member admitted. What
    /// separates a living association from a page somebody made once, and what
    /// <see cref="OrganizationStatus.Active"/> is derived from.
    /// </summary>
    public DateTimeOffset? LastActivityAt { get; private set; }

    /// <summary>Distinct members who have reported it. Drives the auto-flag threshold.</summary>
    public int ReportCount { get; private set; }

    /// <summary>Bumped on every content change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    // ── Auditing & soft delete ──────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    // ── Derived reads ───────────────────────────────────────────────────────────────────────

    public OrganizationMediaReference? CoverImage =>
        _mediaReferences.FirstOrDefault(
            reference => reference.Role == CommunityLifeMediaRole.Cover);

    public OrganizationMediaReference? Logo =>
        _mediaReferences.FirstOrDefault(
            reference => reference.Role == CommunityLifeMediaRole.Logo);

    /// <summary>Whether anybody outside its administrators can reach it.</summary>
    public bool IsDiscoverable =>
        !IsDeleted
        && OrganizationStatusTransitions.IsDiscoverable(Status)
        && Visibility is OrganizationVisibility.Public or OrganizationVisibility.MembersOnly
        && ModerationState is CommunityLifeModerationState.Clear
            or CommunityLifeModerationState.Flagged;

    /// <summary>Whether members may currently join or be invited.</summary>
    public bool AcceptsMembership =>
        !IsDeleted
        && OrganizationStatusTransitions.AcceptsMembership(Status)
        && MembershipPolicy.Kind != MembershipPolicyKind.Closed
        && ModerationState is not (CommunityLifeModerationState.Hidden
            or CommunityLifeModerationState.Suspended);

    /// <summary>Whether events may be created under it.</summary>
    public bool AcceptsEvents =>
        !IsDeleted
        && OrganizationStatusTransitions.AcceptsEvents(Status)
        && ModerationState is not (CommunityLifeModerationState.Hidden
            or CommunityLifeModerationState.Suspended);

    /// <summary>Whether the page has the minimum somebody needs to judge it.</summary>
    public bool IsPublishable =>
        Description.Summary.Length > 0
        && OrganizationTypeKey.Length > 0
        && Location.CountryCode.Length > 0
        && ModerationState is not (CommunityLifeModerationState.Hidden
            or CommunityLifeModerationState.Suspended);

    /// <summary>
    /// How complete the page is, nought to one. Rendered as a prompt to the administrators rather
    /// than as a quality score to members: a thin page is a page nobody has finished, not a bad
    /// organization.
    /// </summary>
    public double Completeness
    {
        get
        {
            var filled = 0;
            var total = 8;

            if (Description.Body is { Length: > 0 }) filled++;
            if (!string.IsNullOrWhiteSpace(Mission)) filled++;
            if (!Contact.IsEmpty) filled++;
            if (!SocialLinks.IsEmpty) filled++;
            if (_leaders.Count > 0) filled++;
            if (_operatingHours.Count > 0) filled++;
            if (_languages.Count > 0) filled++;
            if (_mediaReferences.Count > 0) filled++;

            return (double)filled / total;
        }
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Drafts an organization.
    /// <para>
    /// Always into <see cref="OrganizationStatus.Draft"/> and
    /// <see cref="OrganizationVisibility.Private"/>, whatever the caller asked for. Publishing is a
    /// separate, deliberate act with its own permission and its own checks, and a create that could
    /// publish would let a client skip every one of them.
    /// </para>
    /// </summary>
    public static Result<Organization> Draft(
        OrganizationId id,
        UserId createdByUserId,
        OrganizationName name,
        CommunityLifeSlug slug,
        CommunityLifeDescription description,
        string? organizationTypeKey,
        CommunityLifeLocationContext location,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(createdByUserId);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(slug);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(location);

        var typeKey = NormalizeKey(organizationTypeKey);

        if (typeKey is null)
        {
            return Result.Failure<Organization>(CommunityLifeErrors.OrganizationTypeInvalid);
        }

        var organization = new Organization(
            id, createdByUserId, name, slug, description, typeKey, location, createdAt);

        organization.RaiseDomainEvent(new OrganizationCreatedDomainEvent
        {
            OrganizationId = id,
            CreatedBy = createdByUserId,
            Name = name.Value,
            Slug = slug.Value,
            OrganizationTypeKey = typeKey,
            CountryCode = location.CountryCode,
            NeighborhoodAreaId = location.NeighborhoodAreaId
        });

        return Result.Success(organization);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Content
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Replaces the organization's descriptive content.
    /// <para>
    /// Every parameter is optional and null means "leave it alone", so a client editing one field
    /// need not round-trip the other eight — and cannot blank a field by omitting it, which is the
    /// failure mode of a naive replace.
    /// </para>
    /// </summary>
    public Result<IReadOnlyList<string>> UpdateContent(
        int? baseRevision,
        UserId? changedBy,
        OrganizationName? name = null,
        CommunityLifeDescription? description = null,
        string? mission = null,
        string? vision = null,
        ContactDetails? contact = null,
        SocialLinks? socialLinks = null,
        MembershipPolicy? membershipPolicy = null)
    {
        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return Result.Failure<IReadOnlyList<string>>(guard.Error);
        }

        var changed = new List<string>();

        if (name is not null && name != Name)
        {
            Name = name;
            changed.Add(nameof(Name));
        }

        if (description is not null && description != Description)
        {
            Description = description;
            changed.Add(nameof(Description));
        }

        if (mission is not null)
        {
            var trimmed = Clip(mission, CommunityLifeLimits.MaxMission);

            if (!string.Equals(trimmed, Mission, StringComparison.Ordinal))
            {
                Mission = trimmed;
                changed.Add(nameof(Mission));
            }
        }

        if (vision is not null)
        {
            var trimmed = Clip(vision, CommunityLifeLimits.MaxVision);

            if (!string.Equals(trimmed, Vision, StringComparison.Ordinal))
            {
                Vision = trimmed;
                changed.Add(nameof(Vision));
            }
        }

        if (contact is not null && contact != Contact)
        {
            Contact = contact;
            changed.Add(nameof(Contact));
        }

        if (socialLinks is not null && socialLinks != SocialLinks)
        {
            SocialLinks = socialLinks;
            changed.Add(nameof(SocialLinks));
        }

        if (membershipPolicy is not null && membershipPolicy != MembershipPolicy)
        {
            MembershipPolicy = membershipPolicy;
            changed.Add(nameof(MembershipPolicy));
        }

        if (changed.Count == 0)
        {
            return Result.Success<IReadOnlyList<string>>([]);
        }

        Touch();
        RaiseUpdated(changed, changedBy);

        return Result.Success<IReadOnlyList<string>>(changed);
    }

    /// <summary>Moves the organization to a different place in B4's hierarchy.</summary>
    public Result ChangeLocation(CommunityLifeLocationContext location, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(location);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (location == Location)
        {
            return Result.Success();
        }

        Location = location;

        Touch();
        RaiseUpdated([nameof(Location)], null);

        return Result.Success();
    }

    /// <summary>Moves the organization to a different catalogue type.</summary>
    public Result ChangeType(string? organizationTypeKey, int? baseRevision)
    {
        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var typeKey = NormalizeKey(organizationTypeKey);

        if (typeKey is null)
        {
            return Result.Failure(CommunityLifeErrors.OrganizationTypeInvalid);
        }

        if (string.Equals(typeKey, OrganizationTypeKey, StringComparison.Ordinal))
        {
            return Result.Success();
        }

        OrganizationTypeKey = typeKey;

        Touch();
        RaiseUpdated([nameof(OrganizationTypeKey)], null);

        return Result.Success();
    }

    /// <summary>Links the organization to the B7 provider behind it, or clears the link.</summary>
    public Result SetProvider(Guid? providerId, int? baseRevision)
    {
        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var resolved = providerId == Guid.Empty ? null : providerId;

        if (resolved == ProviderId)
        {
            return Result.Success();
        }

        ProviderId = resolved;

        Touch();
        RaiseUpdated([nameof(ProviderId)], null);

        return Result.Success();
    }

    /// <summary>Replaces the leadership list wholesale, renumbering it from zero.</summary>
    public Result SetLeaders(IReadOnlyList<OrganizationLeader> leaders, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(leaders);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var kept = leaders.Where(leader => leader.Name.Length > 0).ToList();

        if (kept.Count > CommunityLifeLimits.MaxLeaders)
        {
            return Result.Failure(CommunityLifeErrors.LeaderLimit);
        }

        _leaders.Clear();
        _leaders.AddRange(kept);

        Touch();
        RaiseUpdated([nameof(Leaders)], null);

        return Result.Success();
    }

    /// <summary>Replaces the opening hours wholesale.</summary>
    public Result SetOperatingHours(
        IReadOnlyList<OrganizationOperatingHour> hours, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(hours);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (hours.Count > CommunityLifeLimits.MaxOperatingHours)
        {
            return Result.Failure(CommunityLifeErrors.OperatingHoursLimit);
        }

        // A row that opens after it closes is a row nobody can read. Refused rather than silently
        // reordered, because the organization meant one of the two and the platform cannot know
        // which.
        if (hours.Any(hour =>
                hour is { IsClosed: false, OpensAt: { } opens, ClosesAt: { } closes }
                && closes <= opens))
        {
            return Result.Failure(CommunityLifeErrors.OperatingHoursInvalid);
        }

        _operatingHours.Clear();
        _operatingHours.AddRange(hours);

        Touch();
        RaiseUpdated([nameof(OperatingHours)], null);

        return Result.Success();
    }

    /// <summary>Replaces the language list wholesale.</summary>
    public Result SetLanguages(IReadOnlyList<OrganizationLanguage> languages, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(languages);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var deduplicated = languages
            .Where(language => language.Name.Length > 0)
            .GroupBy(language => language.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (deduplicated.Count > CommunityLifeLimits.MaxLanguages)
        {
            return Result.Failure(CommunityLifeErrors.LanguageLimit);
        }

        _languages.Clear();
        _languages.AddRange(deduplicated);

        Touch();
        RaiseUpdated([nameof(Languages)], null);

        return Result.Success();
    }

    /// <summary>Replaces the services list wholesale.</summary>
    public Result SetServices(IReadOnlyList<OrganizationService> services, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(services);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var deduplicated = services
            .Where(service => service.Name.Length > 0)
            .GroupBy(service => service.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (deduplicated.Count > CommunityLifeLimits.MaxServices)
        {
            return Result.Failure(CommunityLifeErrors.ServiceLimit);
        }

        _services.Clear();
        _services.AddRange(deduplicated);

        Touch();
        RaiseUpdated([nameof(Services)], null);

        return Result.Success();
    }

    /// <summary>Replaces the contextual guide references.</summary>
    public Result SetGuideReferences(IReadOnlyList<OrganizationGuideReference> references)
    {
        ArgumentNullException.ThrowIfNull(references);

        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (references.Count > CommunityLifeLimits.MaxGuideReferences)
        {
            return Result.Failure(CommunityLifeErrors.GuideReferenceInvalid);
        }

        _guideReferences.Clear();
        _guideReferences.AddRange(
            references.GroupBy(reference => reference.GuideId).Select(group => group.First()));

        Touch();

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Media
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Attaches a Media asset by reference.
    /// <para>
    /// Idempotent on (media, role): retrying after a timeout gives the same gallery rather than two
    /// of the same photograph. Attaching a second cover or logo replaces the first, because there is
    /// one of each and a page with two would render whichever the database returned first.
    /// </para>
    /// </summary>
    public Result AttachMedia(
        Guid mediaId, CommunityLifeMediaRole role, string? caption, DateTimeOffset now)
    {
        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (mediaId == Guid.Empty)
        {
            return Result.Failure(CommunityLifeErrors.MediaInvalid);
        }

        if (_mediaReferences.Count >= CommunityLifeLimits.MaxMediaReferences)
        {
            return Result.Failure(CommunityLifeErrors.MediaLimit);
        }

        if (_mediaReferences.Any(
                reference => reference.MediaId == mediaId && reference.Role == role))
        {
            return Result.Success();
        }

        if (role is CommunityLifeMediaRole.Cover or CommunityLifeMediaRole.Logo)
        {
            _mediaReferences.RemoveAll(reference => reference.Role == role);
        }

        var order = _mediaReferences.Count(reference => reference.Role == role);

        _mediaReferences.Add(
            OrganizationMediaReference.Create(mediaId, role, order, caption));

        Touch();
        ModifiedAt = now;

        RaiseDomainEvent(new OrganizationMediaAttachedDomainEvent
        {
            OrganizationId = Id,
            MediaId = mediaId,
            Role = role
        });

        return Result.Success();
    }

    /// <summary>Removes a media reference. Never deletes the asset — that is Media's decision.</summary>
    public Result DetachMedia(Guid mediaId)
    {
        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        var reference = _mediaReferences.FirstOrDefault(item => item.MediaId == mediaId);

        if (reference is null)
        {
            return Result.Success();
        }

        _mediaReferences.Remove(reference);
        Touch();

        RaiseDomainEvent(new OrganizationMediaDetachedDomainEvent
        {
            OrganizationId = Id,
            MediaId = mediaId,
            Role = reference.Role
        });

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Submits the page for review without publishing it.</summary>
    public Result Submit(DateTimeOffset now, UserId? submittedBy)
    {
        if (!IsPublishable)
        {
            return Result.Failure(CommunityLifeErrors.OrganizationNotPublishable);
        }

        return MoveTo(OrganizationStatus.Submitted, now, submittedBy, reason: null);
    }

    /// <summary>
    /// Makes the organization discoverable.
    /// <para>
    /// Lands in <see cref="OrganizationStatus.Published"/> rather than
    /// <see cref="OrganizationStatus.Active"/>: activity is something the organization demonstrates
    /// rather than something an editor asserts, and a directory that let every new page claim to be
    /// active would have no way to show members which ones are.
    /// </para>
    /// </summary>
    public Result Publish(DateTimeOffset now, UserId? publishedBy = null)
    {
        if (IsDeleted)
        {
            return Result.Failure(CommunityLifeErrors.OrganizationNotFound);
        }

        if (Status is OrganizationStatus.Published or OrganizationStatus.Active)
        {
            return Result.Failure(CommunityLifeErrors.OrganizationAlreadyPublished);
        }

        if (!IsPublishable)
        {
            return Result.Failure(CommunityLifeErrors.OrganizationNotPublishable);
        }

        var moved = MoveTo(OrganizationStatus.Published, now, publishedBy, reason: null);

        if (moved.IsFailure)
        {
            return moved;
        }

        var isFirstPublication = FirstPublishedAt is null;

        PublishedAt = now;
        FirstPublishedAt ??= now;
        Visibility = Visibility == OrganizationVisibility.Private
            ? OrganizationVisibility.Public
            : Visibility;

        RaiseDomainEvent(new OrganizationPublishedDomainEvent
        {
            OrganizationId = Id,
            Name = Name.Value,
            Slug = Slug.Value,
            OrganizationTypeKey = OrganizationTypeKey,
            CountryCode = Location.CountryCode,
            NeighborhoodAreaId = Location.NeighborhoodAreaId,
            IsFirstPublication = isFirstPublication,
            PublishedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// The general lifecycle move. Every transition goes through here so the machine is consulted
    /// once and the status event is raised once.
    /// </summary>
    public Result MoveTo(
        OrganizationStatus target,
        DateTimeOffset now,
        UserId? changedBy = null,
        string? reason = null)
    {
        if (IsDeleted)
        {
            return Result.Failure(CommunityLifeErrors.OrganizationNotFound);
        }

        if (Status == target)
        {
            return Result.Success();
        }

        if (!OrganizationStatusTransitions.CanTransition(Status, target))
        {
            return Result.Failure(CommunityLifeErrors.OrganizationTransitionInvalid);
        }

        var previous = Status;

        Status = target;
        ModifiedAt = now;

        if (target is OrganizationStatus.Draft or OrganizationStatus.Archived)
        {
            Visibility = OrganizationVisibility.Private;
        }

        RaiseDomainEvent(new OrganizationStatusChangedDomainEvent
        {
            OrganizationId = Id,
            PreviousStatus = previous,
            CurrentStatus = target,
            ChangedBy = changedBy,
            Reason = reason,
            ChangedAt = now
        });

        return Result.Success();
    }

    /// <summary>Withdraws the organization. Reversible only through a restore into draft.</summary>
    public Result Archive(DateTimeOffset now, UserId? changedBy, string? reason) =>
        MoveTo(OrganizationStatus.Archived, now, changedBy, reason);

    /// <summary>
    /// Brings an archived organization back, as a draft.
    /// <para>
    /// Never straight to published: a page that has been away wants its contact details, its
    /// opening hours and its leadership looked at before members are pointed at it again. Most of
    /// what goes stale on an organization page is exactly the part somebody would act on.
    /// </para>
    /// </summary>
    public Result RestoreFromArchive(DateTimeOffset now, UserId? changedBy)
    {
        if (Status != OrganizationStatus.Archived)
        {
            return Result.Failure(CommunityLifeErrors.OrganizationNotArchived);
        }

        var moved = MoveTo(OrganizationStatus.Draft, now, changedBy, reason: null);

        if (moved.IsFailure)
        {
            return moved;
        }

        RaiseDomainEvent(new OrganizationRestoredDomainEvent
        {
            OrganizationId = Id,
            WasDeleted = false
        });

        return Result.Success();
    }

    /// <summary>Changes who can reach the page, independent of its lifecycle state.</summary>
    public Result SetVisibility(OrganizationVisibility visibility, int? baseRevision)
    {
        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (Visibility == visibility)
        {
            return Result.Success();
        }

        Visibility = visibility;

        Touch();
        RaiseUpdated([nameof(Visibility)], null);

        return Result.Success();
    }

    /// <summary>Promotes or demotes the organization. Staff only, gated at the endpoint.</summary>
    public void SetFeatured(bool isFeatured, DateTimeOffset now)
    {
        if (IsFeatured == isFeatured)
        {
            return;
        }

        IsFeatured = isFeatured;
        ModifiedAt = now;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Activity & statistics
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Records that something happened under this organization, and promotes it to
    /// <see cref="OrganizationStatus.Active"/> the first time.
    /// <para>
    /// Deliberately does not bump the revision. A member joining while an administrator is editing
    /// the page must not make the administrator's save fail.
    /// </para>
    /// </summary>
    public void RecordActivity(DateTimeOffset now)
    {
        LastActivityAt = now;
        ModifiedAt = now;

        if (Status == OrganizationStatus.Published)
        {
            // Not through MoveTo: this is a derived promotion rather than an editorial decision,
            // and raising a status-changed event for it would put a machine's bookkeeping into an
            // audit trail members and moderators read.
            Status = OrganizationStatus.Active;
        }
    }

    /// <summary>
    /// Applies a movement to the membership and event counters.
    /// <para>
    /// <b>The single settlement path, called only from the membership and event status
    /// handlers.</b> No command on this aggregate moves a counter directly, which is what makes the
    /// numbers reconcilable: there is exactly one place to read to know how a count can change.
    /// </para>
    /// </summary>
    public void SettleStatistics(
        DateTimeOffset now,
        int members = 0,
        int pending = 0,
        int administrators = 0,
        int volunteers = 0,
        int publishedEvents = 0,
        int completedEvents = 0)
    {
        Statistics = Statistics.Apply(
            members, pending, administrators, volunteers, publishedEvents, completedEvents);

        ModifiedAt = now;
    }

    /// <summary>Restates the membership counters from a recount. The repair path.</summary>
    public void ReconcileStatistics(
        int members, int pending, int administrators, int volunteers, DateTimeOffset now)
    {
        Statistics = Statistics.Reconcile(members, pending, administrators, volunteers);
        ModifiedAt = now;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Moderation & safety
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Records a report and flags the organization when the threshold is met.
    /// <para>
    /// The threshold is passed in rather than read from options here, because the domain must not
    /// know about configuration — and because an impersonation report flags on the first one
    /// regardless, which the caller expresses by passing a threshold of one.
    /// </para>
    /// </summary>
    public bool RecordReport(int reportsBeforeAutoFlag, DateTimeOffset now, UserId? reportedBy)
    {
        ReportCount++;
        ModifiedAt = now;

        if (ModerationState != CommunityLifeModerationState.Clear
            || ReportCount < Math.Max(1, reportsBeforeAutoFlag))
        {
            return false;
        }

        ChangeModeration(
            CommunityLifeModerationState.Flagged,
            reportedBy,
            "Automatically flagged by reports.",
            now);

        return true;
    }

    /// <summary>Removes the page from discovery. Its administrators can still see and fix it.</summary>
    public void Hide(string? reason, UserId? moderatorId, DateTimeOffset now) =>
        ChangeModeration(CommunityLifeModerationState.Hidden, moderatorId, reason, now);

    /// <summary>
    /// Removes the page and bars republication.
    /// <para>
    /// Also moves the lifecycle to <see cref="OrganizationStatus.Suspended"/>, which the hide does
    /// not. A hidden organization is one with a problem on its page; a suspended one is an
    /// institution the platform has stopped vouching for, and its events should stop appearing too.
    /// </para>
    /// </summary>
    public void Suspend(string? reason, UserId? moderatorId, DateTimeOffset now)
    {
        ChangeModeration(CommunityLifeModerationState.Suspended, moderatorId, reason, now);

        if (OrganizationStatusTransitions.CanTransition(Status, OrganizationStatus.Suspended))
        {
            MoveTo(OrganizationStatus.Suspended, now, moderatorId, reason);
        }
    }

    /// <summary>Clears a flag, a hide or a suspension.</summary>
    public void Clear(UserId? moderatorId, DateTimeOffset now)
    {
        ChangeModeration(CommunityLifeModerationState.Clear, moderatorId, null, now);

        if (Status == OrganizationStatus.Suspended)
        {
            MoveTo(OrganizationStatus.Published, now, moderatorId, reason: null);
        }
    }

    /// <summary>
    /// Adds a note to the editorial trail.
    /// <para>
    /// Deliberately not a content edit: it raises no update event and does not bump the revision,
    /// so annotating a page cannot make an administrator's in-flight save fail.
    /// </para>
    /// </summary>
    public void AddModerationNote(Guid noteId, UserId authorId, string? note, DateTimeOffset now)
    {
        if (_moderationNotes.Count >= CommunityLifeLimits.MaxModerationNotes)
        {
            return;
        }

        _moderationNotes.Add(
            OrganizationModerationNote.Create(noteId, authorId, note, now));
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Trust
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Records that a verification claim has been lodged for this organization.</summary>
    public Result SubmitForVerification(
        Guid verificationId, UserId requestedBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(requestedBy);

        if (TrustMark.IsPending)
        {
            return Result.Failure(CommunityLifeErrors.VerificationAlreadyPending);
        }

        TrustMark = CommunityLifeTrustMark.Submitted(verificationId, now);
        ModifiedAt = now;

        RaiseDomainEvent(new OrganizationVerificationRequestedDomainEvent
        {
            OrganizationId = Id,
            RequestedBy = requestedBy,
            VerificationId = verificationId
        });

        return Result.Success();
    }

    /// <summary>
    /// Applies Trust's decision. The only route to a verified organization, and called only from
    /// the Trust outcome handler.
    /// </summary>
    public void ApplyVerificationOutcome(
        OrganizationVerificationState state,
        Guid? verificationId,
        string? badge,
        string? reason,
        DateTimeOffset now)
    {
        var previous = TrustMark.State;

        if (previous == state)
        {
            return;
        }

        TrustMark = CommunityLifeTrustMark.FromOutcome(
            state, verificationId, badge, now, reason);

        ModifiedAt = now;

        RaiseDomainEvent(new OrganizationVerificationChangedDomainEvent
        {
            OrganizationId = Id,
            PreviousState = previous,
            CurrentState = state,
            VerificationId = verificationId,
            Badge = badge,
            Reason = reason,
            ChangedAt = now
        });
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Soft delete & auditing
    // ════════════════════════════════════════════════════════════════════════════════════════

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;

        RaiseDomainEvent(new OrganizationDeletedDomainEvent
        {
            OrganizationId = Id,
            DeletedAt = timestamp
        });
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;

        RaiseDomainEvent(new OrganizationRestoredDomainEvent
        {
            OrganizationId = Id,
            WasDeleted = true
        });
    }

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

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Internals
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Rejects a save made against a revision somebody else has already superseded.</summary>
    public Result EnsureRevision(int? baseRevision) =>
        baseRevision is { } expected && expected != Revision
            ? Result.Failure(CommunityLifeErrors.RevisionConflict)
            : Result.Success();

    private void ChangeModeration(
        CommunityLifeModerationState state,
        UserId? moderatorId,
        string? reason,
        DateTimeOffset now)
    {
        if (ModerationState == state)
        {
            return;
        }

        var previous = ModerationState;

        ModerationState = state;
        ModifiedAt = now;

        RaiseDomainEvent(new OrganizationModerationChangedDomainEvent
        {
            OrganizationId = Id,
            PreviousState = previous,
            CurrentState = state,
            ChangedBy = moderatorId,
            Reason = reason
        });
    }

    private Result EnsureEditable(int? baseRevision)
    {
        if (IsDeleted)
        {
            return Result.Failure(CommunityLifeErrors.OrganizationNotFound);
        }

        if (Status == OrganizationStatus.Archived)
        {
            return Result.Failure(CommunityLifeErrors.OrganizationArchivedImmutable);
        }

        return EnsureRevision(baseRevision);
    }

    private void RaiseUpdated(IReadOnlyList<string> changedFields, UserId? changedBy) =>
        RaiseDomainEvent(new OrganizationUpdatedDomainEvent
        {
            OrganizationId = Id,
            ChangedBy = changedBy,
            ChangedFields = changedFields
        });

    private void Touch() => Revision++;

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }

    /// <summary>
    /// The comparable form of a catalogue key. Shared with the catalogue so a contributor supplying
    /// "Student Association" and an organization supplying "student-association" land on one type.
    /// </summary>
    public static string? NormalizeKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var normalized = key.Trim().ToLowerInvariant().Replace(' ', '-');

        return normalized.Length > CommunityLifeLimits.MaxCategoryKey ? null : normalized;
    }
}
