using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Providers.Domain.Availability;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Events;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Policies;
using Diaspora.Modules.Providers.Domain.Services;
using Diaspora.Modules.Providers.Domain.ValueObjects;
using Diaspora.Modules.Providers.Domain.Verification;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

// The aggregate exposes a collection property called OperatingHours and the entity in it is also
// called OperatingHours. C#'s "colour colour" rule does not cover that pair — it applies only
// when the property's own type shares the name, and here the property is a list — so a static
// call written as OperatingHours.Open would bind to the property and fail. The alias makes the
// type reachable without renaming a domain concept the brief names explicitly.
using WeeklySchedule = Diaspora.Modules.Providers.Domain.Availability.OperatingHours;

namespace Diaspora.Modules.Providers.Domain.Providers;

/// <summary>
/// Somebody who offers something. The aggregate the whole module is arranged around.
/// <para>
/// <b>One aggregate for every kind of provider</b> — an individual braider, a restaurant, a
/// mosque, an embassy, a student association, a clinic. That is the central design commitment of
/// B7, and it is what the brief means by "future modules must never implement their own provider
/// model". The frontend today carries the same concept twice, as an untyped
/// <c>mockListings</c> element and as a typed <c>CommunityOrganization</c>, with overlapping
/// fields and an <c>OrgCategory</c> member literally commented <c>// future-ready</c> where the
/// two collide. Marketplace, Jobs, Housing, Events and Community Life would each have added a
/// third, a fourth and a fifth.
/// </para>
/// <para>
/// <b>What it owns:</b> everything whose lifetime is exactly the provider's and whose
/// consistency has to hold in one transaction — its profile, its organizational detail, its
/// people, its services, its service areas, its branches, its languages, its contact methods,
/// its gallery, its certifications, its tags, its weekly schedule, its dated exceptions and its
/// verification claims. Loading a provider loads its whole profile screen, which is exactly the
/// read the client makes.
/// </para>
/// <para>
/// <b>What it deliberately does not own:</b> reviews and recommendations are separate
/// aggregates keyed by <see cref="ProviderId"/>. A provider with four thousand reviews must not
/// be loaded to add the four-thousand-and-first, and two members reviewing at once must not
/// contend on the provider row. What the provider keeps is the rollup —
/// <see cref="ReviewSummary"/> — which is recomputed rather than incremented. Comments,
/// reactions, reports, shares, follows and bookmarks are not here at all: they belong to B6, and
/// B7 reaches them by registering a content source for
/// <c>CommunityContentType.Listing</c>.
/// </para>
/// <para>
/// <b>Verification is a projection.</b> Nothing on this aggregate can grant a verified badge.
/// <see cref="SubmitVerification"/> records that a claim went to Trust;
/// <see cref="ApplyVerificationOutcome"/> is reachable only from a handler for a Trust
/// integration event. ADR-002's line, applied to the most consequential badge this platform
/// renders.
/// </para>
/// </summary>
public sealed class Provider
    : AggregateRoot<ProviderId>, IAuditableEntity, ISoftDeletable
{
    private readonly List<ProviderMembership> _members = [];
    private readonly List<ProviderService> _services = [];
    private readonly List<ProviderServiceArea> _serviceAreas = [];
    private readonly List<ProviderLocation> _locations = [];
    private readonly List<ProviderLanguage> _languages = [];
    private readonly List<ProviderContactMethod> _contactMethods = [];
    private readonly List<ProviderGalleryItem> _gallery = [];
    private readonly List<ProviderCertification> _certifications = [];
    private readonly List<ProviderTag> _tags = [];
    private readonly List<OperatingHours> _operatingHours = [];
    private readonly List<AvailabilityException> _availabilityExceptions = [];
    private readonly List<ProviderVerification> _verifications = [];

    private Provider(
        ProviderId id,
        ProviderName name,
        ProviderSlug slug,
        ProviderType type,
        ServiceCategoryId categoryId,
        ProviderProfile profile,
        ProviderLocationContext location,
        DateTimeOffset createdAt)
        : base(id)
    {
        Name = name;
        Slug = slug;
        Type = type;
        CategoryId = categoryId;
        Profile = profile;
        Location = location;
        Status = ProviderStatus.Draft;
        Visibility = ProviderVisibility.Public;
        Availability = AvailabilitySettings.Default;
        Statistics = ProviderStatistics.Empty;
        ReviewSummary = ProviderReviewSummary.Empty;
        VerificationSummary = ProviderVerificationSummary.Unverified;
        CreatedAt = createdAt;
    }

    private Provider() { }

    // ── Identity & classification ───────────────────────────────────────────────────────────

    public ProviderName Name { get; private set; } = default!;

    /// <summary>The stable handle a deep link uses. Derived once and never recomputed.</summary>
    public ProviderSlug Slug { get; private set; } = default!;

    public ProviderType Type { get; private set; }

    /// <summary>The catalogue node this provider sits under. Required — a provider with no
    /// category cannot be browsed to, which is the same as not existing.</summary>
    public ServiceCategoryId CategoryId { get; private set; } = default!;

    /// <summary>Where its services happen, by default. Individual services may differ.</summary>
    public ServiceDeliveryMode DeliveryMode { get; private set; } = ServiceDeliveryMode.OnSite;

    // ── Content ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Prose, imagery and the founding story. Always present; may be empty.</summary>
    public ProviderProfile Profile { get; private set; } = default!;

    /// <summary>Registration detail. Null for an individual, and that is the normal case.</summary>
    public ProviderOrganization? Organization { get; private set; }

    /// <summary>The principal address, denormalised from Location's catalogue.</summary>
    public ProviderLocationContext Location { get; private set; } = ProviderLocationContext.None;

    // ── Lifecycle & audience ────────────────────────────────────────────────────────────────

    public ProviderStatus Status { get; private set; }

    public ProviderVisibility Visibility { get; private set; } = ProviderVisibility.Public;

    /// <summary>The client's <c>isFeatured</c>. Staff-set; never something an owner can grant.</summary>
    public bool IsFeatured { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset? StatusChangedAt { get; private set; }

    /// <summary>Why staff suspended it, or why the owner closed it. Shown to the owner.</summary>
    public string? StatusReason { get; private set; }

    // ── Denormalised rollups ────────────────────────────────────────────────────────────────

    public AvailabilitySettings Availability { get; private set; } = AvailabilitySettings.Default;

    public ProviderStatistics Statistics { get; private set; } = ProviderStatistics.Empty;

    public ProviderReviewSummary ReviewSummary { get; private set; } = ProviderReviewSummary.Empty;

    public ProviderVerificationSummary VerificationSummary { get; private set; } =
        ProviderVerificationSummary.Unverified;

    // ── Owned collections ───────────────────────────────────────────────────────────────────

    public IReadOnlyList<ProviderMembership> Members => _members.AsReadOnly();

    public IReadOnlyList<ProviderService> Services =>
        _services.OrderBy(service => service.Position).ToList().AsReadOnly();

    public IReadOnlyList<ProviderServiceArea> ServiceAreas => _serviceAreas.AsReadOnly();

    public IReadOnlyList<ProviderLocation> Locations =>
        _locations.OrderBy(location => location.Position).ToList().AsReadOnly();

    public IReadOnlyList<ProviderLanguage> Languages => _languages.AsReadOnly();

    public IReadOnlyList<ProviderContactMethod> ContactMethods =>
        _contactMethods.OrderBy(method => method.Position).ToList().AsReadOnly();

    public IReadOnlyList<ProviderGalleryItem> Gallery =>
        _gallery.OrderBy(item => item.Position).ToList().AsReadOnly();

    public IReadOnlyList<ProviderCertification> Certifications => _certifications.AsReadOnly();

    public IReadOnlyList<ProviderTag> Tags => _tags.AsReadOnly();

    public IReadOnlyList<OperatingHours> OperatingHours =>
        _operatingHours.OrderBy(hours => hours.Day).ToList().AsReadOnly();

    public IReadOnlyList<AvailabilityException> AvailabilityExceptions =>
        _availabilityExceptions.OrderBy(exception => exception.StartsOn).ToList().AsReadOnly();

    public IReadOnlyList<ProviderVerification> Verifications => _verifications.AsReadOnly();

    // ── Auditing & soft delete ──────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    // ── Derived reads ───────────────────────────────────────────────────────────────────────

    /// <summary>Whether the provider may appear in discovery, search and nearby results.</summary>
    public bool IsDiscoverable =>
        !IsDeleted && Status == ProviderStatus.Active && Visibility.IsDiscoverable;

    /// <summary>Whether it is operating at all, for a client deciding to show a closed banner.</summary>
    public bool IsOperating => Status == ProviderStatus.Active;

    /// <summary>The client's <c>verificationStatus === 'Verified'</c>.</summary>
    public bool IsVerified => VerificationSummary.IsVerified;

    /// <summary>Whether this provider serves people who cannot come to it.</summary>
    public bool ServesRemotely =>
        DeliveryMode is ServiceDeliveryMode.Remote or ServiceDeliveryMode.Hybrid
        || _serviceAreas.Any(area => area.Kind == ServiceAreaKind.Remote);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Registration
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Registers a provider as a draft, owned by its creator.
    /// <para>
    /// A draft rather than a live listing, deliberately. Registration is one screen and a
    /// complete provider is many; publishing straight from registration would put
    /// half-described businesses into the directory, and would give the owner no chance to add
    /// photographs before their first impression is made.
    /// </para>
    /// <para>
    /// The founding owner is added as a membership rather than stored in an owner column, which
    /// is what makes managers, editors and eventual franchises additive rather than a migration.
    /// </para>
    /// </summary>
    public static Result<Provider> Register(
        ProviderId id,
        ProviderName name,
        ProviderSlug slug,
        ProviderType type,
        ServiceCategoryId categoryId,
        UserId ownerId,
        ProviderProfileId profileId,
        ProviderMembershipId ownerMembershipId,
        DateTimeOffset now,
        ProviderLocationContext? location = null,
        ServiceDeliveryMode deliveryMode = ServiceDeliveryMode.OnSite,
        ProviderDescription? description = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(slug);
        ArgumentNullException.ThrowIfNull(categoryId);
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(profileId);
        ArgumentNullException.ThrowIfNull(ownerMembershipId);

        var provider = new Provider(
            id,
            name,
            slug,
            type,
            categoryId,
            ProviderProfile.Create(profileId, description),
            location ?? ProviderLocationContext.None,
            now)
        {
            DeliveryMode = deliveryMode
        };

        provider._members.Add(
            ProviderMembership.Create(ownerMembershipId, ownerId, ProviderRole.Owner, now));

        provider.Statistics = provider.Statistics.WithMemberCount(1);
        provider.RecomputeProfileCompletion();

        provider.RaiseDomainEvent(new ProviderRegisteredDomainEvent
        {
            ProviderId = id,
            OwnerId = ownerId,
            ProviderType = type,
            CategoryId = categoryId,
            Name = name.Value,
            Slug = slug.Value,
            CountryCode = provider.Location.CountryCode,
            AreaId = provider.Location.AreaId
        });

        provider.RaiseDomainEvent(new ProviderMemberAddedDomainEvent
        {
            ProviderId = id,
            UserId = ownerId,
            Role = ProviderRole.Owner
        });

        return Result.Success(provider);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Core content
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Renames, recategorises or reclassifies the provider.</summary>
    public Result Update(
        ProviderName name,
        ServiceCategoryId categoryId,
        ProviderType type,
        ServiceDeliveryMode deliveryMode,
        UserId? changedBy = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(categoryId);

        if (!CanBeEdited)
        {
            return Result.Failure(ProviderErrors.NotEditable);
        }

        var changed = new List<string>();

        if (!Name.Equals(name))
        {
            Name = name;
            changed.Add("name");
        }

        if (!CategoryId.Equals(categoryId))
        {
            CategoryId = categoryId;
            changed.Add("category");
        }

        if (Type != type)
        {
            Type = type;
            changed.Add("providerType");
        }

        if (DeliveryMode != deliveryMode)
        {
            DeliveryMode = deliveryMode;
            changed.Add("deliveryMode");
        }

        if (changed.Count > 0)
        {
            RecomputeProfileCompletion();
            RaiseUpdated(changed, changedBy);
        }

        return Result.Success();
    }

    /// <summary>
    /// Changes the deep-link handle.
    /// <para>
    /// Its own operation rather than part of <see cref="Update"/>, because a rename must
    /// <em>not</em> silently change the slug. Every printed flyer, WhatsApp share and search
    /// result pointing at the old one would break, and the person renaming their business has no
    /// idea that is what they did.
    /// </para>
    /// </summary>
    public Result ChangeSlug(ProviderSlug slug, UserId? changedBy = null)
    {
        ArgumentNullException.ThrowIfNull(slug);

        if (!CanBeEdited)
        {
            return Result.Failure(ProviderErrors.NotEditable);
        }

        if (Slug.Equals(slug))
        {
            return Result.Success();
        }

        Slug = slug;
        RaiseUpdated(["slug"], changedBy);

        return Result.Success();
    }

    public Result UpdateProfile(
        ProviderDescription description,
        string? tagline,
        string? mission,
        DateOnly? foundedOn,
        DateOnly today,
        UserId? changedBy = null)
    {
        if (!CanBeEdited)
        {
            return Result.Failure(ProviderErrors.NotEditable);
        }

        var updated = Profile.Update(description, tagline, mission, foundedOn, today);

        if (updated.IsFailure)
        {
            return updated;
        }

        RecomputeProfileCompletion();
        RaiseUpdated(["description", "tagline", "mission", "foundedOn"], changedBy);

        return Result.Success();
    }

    /// <summary>
    /// Sets the organizational block, creating it on first use.
    /// <para>
    /// Created lazily rather than at registration because most providers never need one, and an
    /// always-present block of nine nulls is a model that reads as "a business, possibly missing
    /// details" rather than "a person or a business or a church".
    /// </para>
    /// </summary>
    public Result UpdateOrganization(
        ProviderOrganizationId organizationId,
        string? legalName,
        string? registrationNumber,
        string? registrationJurisdiction,
        DateOnly? registeredOn,
        string? taxIdentifier,
        bool isNonProfit,
        int? membershipSize,
        int? staffSize,
        UserId? changedBy = null)
    {
        ArgumentNullException.ThrowIfNull(organizationId);

        if (!CanBeEdited)
        {
            return Result.Failure(ProviderErrors.NotEditable);
        }

        Organization ??= ProviderOrganization.Create(organizationId);

        var updated = Organization.Update(
            legalName,
            registrationNumber,
            registrationJurisdiction,
            registeredOn,
            taxIdentifier,
            isNonProfit,
            membershipSize,
            staffSize);

        if (updated.IsFailure)
        {
            return updated;
        }

        RecomputeProfileCompletion();
        RaiseUpdated(["organization"], changedBy);

        return Result.Success();
    }

    /// <summary>Sets the principal address from a scope Location resolved.</summary>
    public Result SetLocation(ProviderLocationContext location, UserId? changedBy = null)
    {
        ArgumentNullException.ThrowIfNull(location);

        if (!CanBeEdited)
        {
            return Result.Failure(ProviderErrors.NotEditable);
        }

        if (Location.Equals(location))
        {
            return Result.Success();
        }

        Location = location;
        RecomputeProfileCompletion();
        RaiseUpdated(["location"], changedBy);

        return Result.Success();
    }

    /// <summary>
    /// Replaces the whole tag list.
    /// <para>
    /// Wholesale rather than add-and-remove because that is how the client's chip editor works —
    /// a person types a set of tags and saves — and a per-tag API would make the common case
    /// three round trips and a partial-failure story.
    /// </para>
    /// </summary>
    public Result ReplaceTags(IEnumerable<(ProviderTagId Id, string Label)> tags, UserId? changedBy = null)
    {
        ArgumentNullException.ThrowIfNull(tags);

        if (!CanBeEdited)
        {
            return Result.Failure(ProviderErrors.NotEditable);
        }

        var resolved = new List<ProviderTag>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (tagId, label) in tags)
        {
            var tag = ProviderTag.Create(tagId, label);

            if (tag is not null && seen.Add(tag.Slug))
            {
                resolved.Add(tag);
            }

            if (resolved.Count >= ProviderLimits.MaxTags)
            {
                break;
            }
        }

        _tags.Clear();
        _tags.AddRange(resolved);

        RaiseUpdated(["tags"], changedBy);

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Makes the provider visible.
    /// <para>
    /// Guarded by <see cref="IsPublishable"/> rather than published unconditionally, because a
    /// listing with no way to be contacted is worse than no listing: a member finds it, wants
    /// it, and has nothing to tap. The bar is deliberately low — a name, a category and one
    /// contact method — so it is a nudge and not a gate.
    /// </para>
    /// </summary>
    public Result Publish(DateTimeOffset now, UserId? publishedBy = null)
    {
        if (IsDeleted)
        {
            return Result.Failure(ProviderErrors.Removed);
        }

        if (Status == ProviderStatus.Suspended)
        {
            return Result.Failure(ProviderErrors.Suspended);
        }

        if (Status == ProviderStatus.Active)
        {
            return Result.Failure(ProviderErrors.AlreadyPublished);
        }

        if (!IsPublishable)
        {
            return Result.Failure(ProviderErrors.NotPublishable);
        }

        var previous = Status;
        var isFirstPublication = PublishedAt is null;

        Status = ProviderStatus.Active;
        StatusChangedAt = now;
        StatusReason = null;

        if (isFirstPublication)
        {
            PublishedAt = now;

            RaiseDomainEvent(new ProviderPublishedDomainEvent
            {
                ProviderId = Id,
                OwnerId = PrimaryOwnerId,
                ProviderType = Type,
                CategoryId = CategoryId,
                Name = Name.Value,
                Visibility = Visibility.Level,
                CountryCode = Location.CountryCode,
                AreaId = Location.AreaId,
                Latitude = Location.Latitude,
                Longitude = Location.Longitude
            });
        }

        RaiseStatusChanged(previous, publishedBy);

        return Result.Success();
    }

    /// <summary>Temporarily stops operating, by the owner's choice. Stays discoverable, marked closed.</summary>
    public Result Pause(DateTimeOffset now, string? reason = null, UserId? changedBy = null) =>
        MoveStatus(ProviderStatus.Paused, now, reason, changedBy, allowFromSuspended: false);

    /// <summary>Permanently stops operating, by the owner's choice. Reviews survive.</summary>
    public Result Close(DateTimeOffset now, string? reason = null, UserId? changedBy = null) =>
        MoveStatus(ProviderStatus.Closed, now, reason, changedBy, allowFromSuspended: false);

    /// <summary>
    /// Withdraws the provider from discovery, by staff.
    /// <para>
    /// Reachable from any status including <see cref="ProviderStatus.Suspended"/> itself, so a
    /// second suspension with a new reason is possible — which is what happens when a provider
    /// appeals, is reinstated, and reoffends.
    /// </para>
    /// </summary>
    public Result Suspend(DateTimeOffset now, string reason, UserId? changedBy = null)
    {
        if (IsDeleted)
        {
            return Result.Failure(ProviderErrors.Removed);
        }

        var previous = Status;

        Status = ProviderStatus.Suspended;
        StatusChangedAt = now;
        StatusReason = Truncate(reason, 500);

        RaiseStatusChanged(previous, changedBy, StatusReason);

        return Result.Success();
    }

    /// <summary>
    /// Lifts a suspension, returning the provider to where it was.
    /// <para>
    /// To <see cref="ProviderStatus.Active"/> when it had published, and to
    /// <see cref="ProviderStatus.Draft"/> when it had not — the state it can meaningfully return
    /// to, rather than a blanket Active that would publish a provider that never was.
    /// </para>
    /// </summary>
    public Result Reinstate(DateTimeOffset now, UserId? changedBy = null)
    {
        if (IsDeleted)
        {
            return Result.Failure(ProviderErrors.Removed);
        }

        if (Status != ProviderStatus.Suspended)
        {
            return Result.Success();
        }

        var previous = Status;

        Status = PublishedAt is null ? ProviderStatus.Draft : ProviderStatus.Active;
        StatusChangedAt = now;
        StatusReason = null;

        RaiseStatusChanged(previous, changedBy);

        return Result.Success();
    }

    /// <summary>Removes the provider from every surface, retaining it for audit.</summary>
    public Result Archive(DateTimeOffset now, string? reason = null, UserId? changedBy = null) =>
        MoveStatus(ProviderStatus.Archived, now, reason, changedBy, allowFromSuspended: true);

    /// <summary>Staff-only. Featured placement is not something an owner can grant themselves.</summary>
    public void SetFeatured(bool isFeatured) => IsFeatured = isFeatured;

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Ownership
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>The role a member holds here, or null when they hold none.</summary>
    public ProviderRole? RoleOf(UserId userId) =>
        _members.SingleOrDefault(member => member.UserId == userId)?.Role;

    /// <summary>Whether a member holds at least the given authority.</summary>
    public bool HasAtLeast(UserId? userId, ProviderRole role) =>
        userId is not null && RoleOf(userId) is { } held && held >= role;

    /// <summary>
    /// The owner discovery and events attribute the provider to.
    /// <para>
    /// The earliest-joined owner, so the answer is stable across restarts and does not change
    /// when a co-owner is added. A provider always has at least one owner, which is the
    /// invariant <see cref="RemoveMember"/> and <see cref="ChangeMemberRole"/> defend.
    /// </para>
    /// </summary>
    public UserId PrimaryOwnerId =>
        NotifiableOwnerId
        ?? throw new InvalidOperationException("A provider always has at least one owner.");

    /// <summary>
    /// The same owner, or null when the aggregate was loaded without its membership.
    /// <para>
    /// <b>What every outgoing event uses, and <see cref="PrimaryOwnerId"/> is not.</b> A
    /// projection that loaded a provider narrowly must still be able to publish: a notification
    /// nobody receives is a far better outcome than a successful write that throws on its way
    /// out because a collection was not included. The invariant still holds — this returns null
    /// for a partial read, never for a provider with no owner.
    /// </para>
    /// </summary>
    public UserId? NotifiableOwnerId =>
        _members
            .Where(member => member.Role == ProviderRole.Owner)
            .OrderBy(member => member.JoinedAt)
            .Select(member => member.UserId)
            .FirstOrDefault();

    public Result<ProviderMembership> AddMember(
        ProviderMembershipId membershipId,
        UserId userId,
        ProviderRole role,
        DateTimeOffset now,
        UserId invitedBy,
        string? title = null,
        bool isPubliclyListed = false)
    {
        ArgumentNullException.ThrowIfNull(membershipId);
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(invitedBy);

        if (_members.Count >= ProviderLimits.MaxMembers)
        {
            return Result.Failure<ProviderMembership>(ProviderErrors.MemberLimitReached);
        }

        if (_members.Any(member => member.UserId == userId))
        {
            return Result.Failure<ProviderMembership>(ProviderErrors.MemberDuplicate);
        }

        // Nobody may hand out authority they do not hold. Without this an editor could appoint
        // an owner, which is privilege escalation dressed as a staff invitation.
        if (!HasAtLeast(invitedBy, ProviderRole.Manager) || RoleOf(invitedBy) < role)
        {
            return Result.Failure<ProviderMembership>(ProviderErrors.MemberRoleInsufficient);
        }

        var membership = ProviderMembership.Create(
            membershipId, userId, role, now, invitedBy, title, isPubliclyListed);

        _members.Add(membership);
        Statistics = Statistics.WithMemberCount(_members.Count);

        RaiseDomainEvent(new ProviderMemberAddedDomainEvent
        {
            ProviderId = Id,
            UserId = userId,
            Role = role,
            InvitedBy = invitedBy
        });

        return Result.Success(membership);
    }

    public Result ChangeMemberRole(UserId userId, ProviderRole role, UserId changedBy)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(changedBy);

        var membership = _members.SingleOrDefault(member => member.UserId == userId);

        if (membership is null)
        {
            return Result.Failure(ProviderErrors.MemberNotFound);
        }

        var actorRole = RoleOf(changedBy);

        if (actorRole is not { } actual || actual < ProviderRole.Manager)
        {
            return Result.Failure(ProviderErrors.MemberRoleInsufficient);
        }

        // A manager may not promote somebody above themselves, nor demote somebody above them.
        if (actual < role || actual < membership.Role)
        {
            return Result.Failure(ProviderErrors.MemberRoleInsufficient);
        }

        if (membership.Role == role)
        {
            return Result.Success();
        }

        // The last-owner rule is checked before the self-demotion rule, and the order matters
        // for the message rather than the outcome. A sole owner demoting themselves trips both;
        // telling them "ask another owner to do it" is advice they cannot follow, because there
        // is no other owner. "A provider must keep at least one owner" tells them what to do.
        if (membership.Role == ProviderRole.Owner && role != ProviderRole.Owner && OwnerCount == 1)
        {
            return Result.Failure(ProviderErrors.MemberLastOwner);
        }

        // Reducing your own authority is refused rather than allowed-and-regretted: somebody who
        // demotes themselves by mistake may have no way back, and by this point there is
        // demonstrably another owner or manager who can do it deliberately.
        if (userId == changedBy && role < membership.Role)
        {
            return Result.Failure(ProviderErrors.MemberSelfDemotion);
        }

        var previous = membership.Role;
        membership.ChangeRole(role);

        RaiseDomainEvent(new ProviderMemberRoleChangedDomainEvent
        {
            ProviderId = Id,
            UserId = userId,
            PreviousRole = previous,
            CurrentRole = role,
            ChangedBy = changedBy
        });

        return Result.Success();
    }

    public Result RemoveMember(UserId userId, UserId removedBy)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(removedBy);

        var membership = _members.SingleOrDefault(member => member.UserId == userId);

        if (membership is null)
        {
            return Result.Failure(ProviderErrors.MemberNotFound);
        }

        var isSelfRemoval = userId == removedBy;

        if (!isSelfRemoval)
        {
            var actorRole = RoleOf(removedBy);

            if (actorRole is not { } actual || actual < ProviderRole.Manager || actual < membership.Role)
            {
                return Result.Failure(ProviderErrors.MemberRoleInsufficient);
            }
        }

        if (membership.Role == ProviderRole.Owner && OwnerCount == 1)
        {
            return Result.Failure(ProviderErrors.MemberLastOwner);
        }

        _members.Remove(membership);
        Statistics = Statistics.WithMemberCount(_members.Count);

        RaiseDomainEvent(new ProviderMemberRemovedDomainEvent
        {
            ProviderId = Id,
            UserId = userId,
            PreviousRole = membership.Role,
            RemovedBy = removedBy
        });

        return Result.Success();
    }

    /// <summary>
    /// Hands the provider to somebody else.
    /// <para>
    /// One operation rather than two role changes, because a business being sold is one thing
    /// that happened and audit, notification and fraud review all want to see it that way. The
    /// previous owner may stay on as a manager (a retiring founder) or leave entirely (a sale).
    /// </para>
    /// </summary>
    public Result TransferOwnership(
        UserId newOwnerId,
        UserId currentOwnerId,
        DateTimeOffset now,
        ProviderMembershipId membershipIdIfNew,
        bool retainPreviousOwner = true)
    {
        ArgumentNullException.ThrowIfNull(newOwnerId);
        ArgumentNullException.ThrowIfNull(currentOwnerId);
        ArgumentNullException.ThrowIfNull(membershipIdIfNew);

        var current = _members.SingleOrDefault(member => member.UserId == currentOwnerId);

        if (current is null || current.Role != ProviderRole.Owner)
        {
            return Result.Failure(ProviderErrors.MemberRoleInsufficient);
        }

        if (newOwnerId == currentOwnerId)
        {
            return Result.Success();
        }

        var incoming = _members.SingleOrDefault(member => member.UserId == newOwnerId);

        if (incoming is null)
        {
            if (_members.Count >= ProviderLimits.MaxMembers)
            {
                return Result.Failure(ProviderErrors.MemberLimitReached);
            }

            incoming = ProviderMembership.Create(
                membershipIdIfNew, newOwnerId, ProviderRole.Owner, now, currentOwnerId);

            _members.Add(incoming);
        }
        else
        {
            incoming.ChangeRole(ProviderRole.Owner);
        }

        if (retainPreviousOwner)
        {
            current.ChangeRole(ProviderRole.Manager);
        }
        else
        {
            _members.Remove(current);
        }

        Statistics = Statistics.WithMemberCount(_members.Count);

        RaiseDomainEvent(new ProviderOwnershipTransferredDomainEvent
        {
            ProviderId = Id,
            PreviousOwnerId = currentOwnerId,
            NewOwnerId = newOwnerId,
            PreviousOwnerRetained = retainPreviousOwner
        });

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Services
    // ════════════════════════════════════════════════════════════════════════════════════════

    public Result<ProviderService> AddService(
        ProviderServiceId serviceId,
        string? name,
        ProviderPricing? pricing = null,
        string? description = null,
        ServiceCategoryId? categoryId = null,
        ServiceDeliveryMode? deliveryMode = null,
        int? durationMinutes = null,
        UserId? addedBy = null)
    {
        if (_services.Count >= ProviderLimits.MaxServices)
        {
            return Result.Failure<ProviderService>(ProviderErrors.ServiceLimitReached);
        }

        var service = ProviderService.Create(
            serviceId,
            name,
            pricing,
            description,
            categoryId,
            deliveryMode ?? DeliveryMode,
            durationMinutes,
            _services.Count);

        if (service.IsFailure)
        {
            return service;
        }

        if (_services.Any(existing =>
                string.Equals(existing.DedupeKey, service.Value.DedupeKey, StringComparison.Ordinal)))
        {
            return Result.Failure<ProviderService>(ProviderErrors.ServiceDuplicate);
        }

        _services.Add(service.Value);
        Statistics = Statistics.WithServiceCount(_services.Count);
        RecomputeProfileCompletion();

        RaiseDomainEvent(new ProviderServiceAddedDomainEvent
        {
            ProviderId = Id,
            ServiceId = serviceId,
            Name = service.Value.Name,
            CategoryId = categoryId,
            AddedBy = addedBy
        });

        return service;
    }

    public Result UpdateService(
        ProviderServiceId serviceId,
        string? name,
        ProviderPricing pricing,
        string? description,
        ServiceCategoryId? categoryId,
        ServiceDeliveryMode deliveryMode,
        int? durationMinutes,
        UserId? changedBy = null)
    {
        var service = _services.SingleOrDefault(candidate => candidate.Id == serviceId);

        if (service is null)
        {
            return Result.Failure(ProviderErrors.ServiceNotFound);
        }

        var updated = service.Update(
            name, pricing, description, categoryId, deliveryMode, durationMinutes);

        if (updated.IsFailure)
        {
            return updated;
        }

        RaiseUpdated(["services"], changedBy);

        return Result.Success();
    }

    public Result RemoveService(ProviderServiceId serviceId, UserId? removedBy = null)
    {
        var service = _services.SingleOrDefault(candidate => candidate.Id == serviceId);

        if (service is null)
        {
            return Result.Failure(ProviderErrors.ServiceNotFound);
        }

        _services.Remove(service);
        ResequenceServices();
        Statistics = Statistics.WithServiceCount(_services.Count);
        RecomputeProfileCompletion();

        RaiseDomainEvent(new ProviderServiceRemovedDomainEvent
        {
            ProviderId = Id,
            ServiceId = serviceId,
            Name = service.Name,
            RemovedBy = removedBy
        });

        return Result.Success();
    }

    public ProviderService? FindService(ProviderServiceId serviceId) =>
        _services.SingleOrDefault(service => service.Id == serviceId);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Service areas, branches, languages, contacts, gallery, certifications
    // ════════════════════════════════════════════════════════════════════════════════════════

    public Result AddServiceArea(ProviderServiceArea area, UserId? changedBy = null)
    {
        ArgumentNullException.ThrowIfNull(area);

        if (_serviceAreas.Count >= ProviderLimits.MaxServiceAreas)
        {
            return Result.Failure(ProviderErrors.ServiceAreaLimitReached);
        }

        if (_serviceAreas.Any(existing =>
                string.Equals(existing.DedupeKey, area.DedupeKey, StringComparison.Ordinal)))
        {
            return Result.Failure(ProviderErrors.ServiceAreaDuplicate);
        }

        _serviceAreas.Add(area);
        RaiseServiceAreasChanged(changedBy);

        return Result.Success();
    }

    public Result RemoveServiceArea(ProviderServiceAreaId areaId, UserId? changedBy = null)
    {
        var area = _serviceAreas.SingleOrDefault(candidate => candidate.Id == areaId);

        if (area is null)
        {
            return Result.Failure(ProviderErrors.ServiceAreaNotFound);
        }

        _serviceAreas.Remove(area);
        RaiseServiceAreasChanged(changedBy);

        return Result.Success();
    }

    public Result AddLocation(ProviderLocation location, UserId? changedBy = null)
    {
        ArgumentNullException.ThrowIfNull(location);

        if (_locations.Count >= ProviderLimits.MaxLocations)
        {
            return Result.Failure(ProviderErrors.ServiceAreaLimitReached);
        }

        if (location.IsPrimary)
        {
            foreach (var existing in _locations)
            {
                existing.SetPrimary(false);
            }
        }

        location.SetPosition(_locations.Count);
        _locations.Add(location);
        RaiseUpdated(["locations"], changedBy);

        return Result.Success();
    }

    public Result RemoveLocation(ProviderLocationId locationId, UserId? changedBy = null)
    {
        var location = _locations.SingleOrDefault(candidate => candidate.Id == locationId);

        if (location is null)
        {
            return Result.Failure(ProviderErrors.ServiceAreaNotFound);
        }

        _locations.Remove(location);
        RaiseUpdated(["locations"], changedBy);

        return Result.Success();
    }

    public Result AddLanguage(ProviderLanguage language, UserId? changedBy = null)
    {
        ArgumentNullException.ThrowIfNull(language);

        if (_languages.Count >= ProviderLimits.MaxLanguages)
        {
            return Result.Failure(ProviderErrors.LanguageLimitReached);
        }

        if (_languages.Any(existing =>
                string.Equals(existing.Code, language.Code, StringComparison.Ordinal)))
        {
            return Result.Failure(ProviderErrors.LanguageDuplicate);
        }

        if (language.IsPrimary)
        {
            foreach (var existing in _languages)
            {
                existing.SetPrimary(false);
            }
        }

        _languages.Add(language);
        RecomputeProfileCompletion();
        RaiseUpdated(["languages"], changedBy);

        return Result.Success();
    }

    public Result RemoveLanguage(ProviderLanguageId languageId, UserId? changedBy = null)
    {
        var language = _languages.SingleOrDefault(candidate => candidate.Id == languageId);

        if (language is null)
        {
            return Result.Failure(ProviderErrors.LanguageNotFound);
        }

        _languages.Remove(language);
        RecomputeProfileCompletion();
        RaiseUpdated(["languages"], changedBy);

        return Result.Success();
    }

    public Result AddContactMethod(ProviderContactMethod method, UserId? changedBy = null)
    {
        ArgumentNullException.ThrowIfNull(method);

        if (_contactMethods.Count >= ProviderLimits.MaxContactMethods)
        {
            return Result.Failure(ProviderErrors.ContactMethodLimitReached);
        }

        if (_contactMethods.Any(existing =>
                string.Equals(existing.DedupeKey, method.DedupeKey, StringComparison.Ordinal)))
        {
            return Result.Failure(ProviderErrors.ContactMethodDuplicate);
        }

        // One primary per kind, so the client's single "Call" button always has an answer and
        // never has two.
        if (method.IsPrimary)
        {
            foreach (var existing in _contactMethods.Where(candidate => candidate.Kind == method.Kind))
            {
                existing.SetPrimary(false);
            }
        }
        else if (_contactMethods.TrueForAll(candidate => candidate.Kind != method.Kind))
        {
            method.SetPrimary(true);
        }

        method.SetPosition(_contactMethods.Count);
        _contactMethods.Add(method);
        RecomputeProfileCompletion();
        RaiseUpdated(["contactMethods"], changedBy);

        return Result.Success();
    }

    public Result RemoveContactMethod(ProviderContactMethodId methodId, UserId? changedBy = null)
    {
        var method = _contactMethods.SingleOrDefault(candidate => candidate.Id == methodId);

        if (method is null)
        {
            return Result.Failure(ProviderErrors.ContactMethodNotFound);
        }

        _contactMethods.Remove(method);

        // Removing the primary promotes the next of the same kind, rather than leaving the kind
        // present with no primary — which would give the client a button with nothing behind it.
        if (method.IsPrimary
            && _contactMethods.FirstOrDefault(candidate => candidate.Kind == method.Kind) is { } successor)
        {
            successor.SetPrimary(true);
        }

        RecomputeProfileCompletion();
        RaiseUpdated(["contactMethods"], changedBy);

        return Result.Success();
    }

    public Result AddGalleryItem(ProviderGalleryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (_gallery.Count >= ProviderLimits.MaxGalleryItems)
        {
            return Result.Failure(ProviderErrors.GalleryLimitReached);
        }

        if (_gallery.Any(existing =>
                existing.MediaId == item.MediaId
                && string.Equals(existing.Role, item.Role, StringComparison.Ordinal)))
        {
            return Result.Failure(ProviderErrors.GalleryDuplicate);
        }

        item.SetPosition(_gallery.Count(existing =>
            string.Equals(existing.Role, item.Role, StringComparison.Ordinal)));

        _gallery.Add(item);
        RecomputeProfileCompletion();

        RaiseDomainEvent(new ProviderMediaAttachedDomainEvent
        {
            ProviderId = Id,
            MediaId = item.MediaId,
            Role = item.Role,
            Position = item.Position
        });

        return Result.Success();
    }

    public Result RemoveGalleryItem(ProviderGalleryItemId itemId)
    {
        var item = _gallery.SingleOrDefault(candidate => candidate.Id == itemId);

        if (item is null)
        {
            return Result.Failure(ProviderErrors.GalleryItemNotFound);
        }

        _gallery.Remove(item);
        ResequenceGallery();
        RecomputeProfileCompletion();

        return Result.Success();
    }

    /// <summary>Sets the cover image and raises the claim so Media does not sweep the asset.</summary>
    public void SetCoverMedia(MediaId? mediaId)
    {
        Profile.SetCover(mediaId);
        RecomputeProfileCompletion();

        if (mediaId is not null)
        {
            RaiseDomainEvent(new ProviderMediaAttachedDomainEvent
            {
                ProviderId = Id,
                MediaId = mediaId,
                Role = ProviderMediaRole.Cover,
                Position = 0
            });
        }
    }

    /// <summary>Sets the logo and raises the claim so Media does not sweep the asset.</summary>
    public void SetLogoMedia(MediaId? mediaId)
    {
        Profile.SetLogo(mediaId);
        RecomputeProfileCompletion();

        if (mediaId is not null)
        {
            RaiseDomainEvent(new ProviderMediaAttachedDomainEvent
            {
                ProviderId = Id,
                MediaId = mediaId,
                Role = ProviderMediaRole.Logo,
                Position = 0
            });
        }
    }

    public Result AddCertification(ProviderCertification certification)
    {
        ArgumentNullException.ThrowIfNull(certification);

        if (_certifications.Count >= ProviderLimits.MaxCertifications)
        {
            return Result.Failure(ProviderErrors.CertificationLimitReached);
        }

        _certifications.Add(certification);
        RecomputeProfileCompletion();

        return Result.Success();
    }

    public Result RemoveCertification(ProviderCertificationId certificationId)
    {
        var certification = _certifications.SingleOrDefault(candidate => candidate.Id == certificationId);

        if (certification is null)
        {
            return Result.Failure(ProviderErrors.CertificationNotFound);
        }

        _certifications.Remove(certification);
        RecomputeProfileCompletion();

        return Result.Success();
    }

    public ProviderCertification? FindCertification(ProviderCertificationId certificationId) =>
        _certifications.SingleOrDefault(certification => certification.Id == certificationId);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Availability
    // ════════════════════════════════════════════════════════════════════════════════════════

    public Result ConfigureAvailability(AvailabilitySettings? settings, UserId? changedBy = null)
    {
        if (settings is null)
        {
            return Result.Failure(ProviderErrors.TimeZoneInvalid);
        }

        Availability = settings;

        RaiseDomainEvent(new ProviderAvailabilityChangedDomainEvent
        {
            ProviderId = Id,
            Kind = settings.Kind,
            AffectsToday = true,
            ChangedBy = changedBy
        });

        return Result.Success();
    }

    /// <summary>Returns the schedule for a day, creating an unset one on first use.</summary>
    public WeeklySchedule HoursFor(DayOfWeek day, OperatingHoursId idIfNew)
    {
        ArgumentNullException.ThrowIfNull(idIfNew);

        var hours = _operatingHours.SingleOrDefault(candidate => candidate.Day == day);

        if (hours is not null)
        {
            return hours;
        }

        hours = WeeklySchedule.Open(idIfNew, day);
        _operatingHours.Add(hours);

        return hours;
    }

    /// <summary>
    /// Replaces one day's schedule wholesale.
    /// <para>
    /// Wholesale rather than per-period, because the client's hours editor is a week of rows and
    /// a person edits a day, not a window. It also makes the overlap check trivially correct:
    /// the periods are validated against each other as a set, rather than each against whatever
    /// happened to be stored when it was added.
    /// </para>
    /// </summary>
    public Result SetDayHours(
        DayOfWeek day,
        bool isClosed,
        IReadOnlyList<(AvailabilityWindowId Id, TimeOnly OpensAt, TimeOnly ClosesAt, string? Label)> windows,
        OperatingHoursId idIfNew,
        UserId? changedBy = null)
    {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(idIfNew);

        var hours = HoursFor(day, idIfNew);

        if (isClosed)
        {
            hours.Close();
            RaiseAvailabilityChanged(changedBy, affectsToday: true);
            return Result.Success();
        }

        hours.Clear();

        foreach (var (windowId, opensAt, closesAt, label) in windows)
        {
            var added = hours.AddWindow(windowId, opensAt, closesAt, label);

            if (added.IsFailure)
            {
                return Result.Failure(added.Error);
            }
        }

        RaiseAvailabilityChanged(changedBy, affectsToday: true);

        return Result.Success();
    }

    public Result AddAvailabilityException(AvailabilityException exception, DateOnly today, UserId? changedBy = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (_availabilityExceptions.Count >= ProviderLimits.MaxAvailabilityExceptions)
        {
            return Result.Failure(ProviderErrors.AvailabilityExceptionLimit);
        }

        if (_availabilityExceptions.Any(existing => existing.OverlapsWith(exception)))
        {
            return Result.Failure(ProviderErrors.AvailabilityExceptionOverlap);
        }

        _availabilityExceptions.Add(exception);

        RaiseAvailabilityChanged(
            changedBy,
            affectsToday: exception.Covers(today),
            effectiveFrom: exception.StartsOn);

        return Result.Success();
    }

    public Result RemoveAvailabilityException(
        AvailabilityExceptionId exceptionId, DateOnly today, UserId? changedBy = null)
    {
        var exception = _availabilityExceptions.SingleOrDefault(candidate => candidate.Id == exceptionId);

        if (exception is null)
        {
            return Result.Failure(ProviderErrors.AvailabilityExceptionNotFound);
        }

        _availabilityExceptions.Remove(exception);
        RaiseAvailabilityChanged(changedBy, affectsToday: exception.Covers(today));

        return Result.Success();
    }

    /// <summary>
    /// Whether the provider is open at an instant, in its own time zone.
    /// <para>
    /// Null when "open" is not a meaningful question — an appointment-only consultant is neither
    /// open nor closed, and returning false would make the client render "Closed" at ten in the
    /// morning on a Tuesday.
    /// </para>
    /// <para>
    /// A dated exception beats the weekly schedule, which is the whole reason exceptions exist:
    /// a shop whose Monday hours say 09:00–17:00 is shut on a bank holiday Monday, and a
    /// calculation that only consults the weekly schedule sends a member to a locked door.
    /// </para>
    /// </summary>
    public bool? IsOpenAt(DateTimeOffset instant)
    {
        if (Status is not ProviderStatus.Active)
        {
            return false;
        }

        if (Availability.Kind == AvailabilityKind.AlwaysOpen)
        {
            return true;
        }

        if (Availability.Kind == AvailabilityKind.Unavailable)
        {
            return false;
        }

        if (!Availability.HasOpenNowSemantics)
        {
            return null;
        }

        var local = Availability.ToLocal(instant);
        var localDate = DateOnly.FromDateTime(local.DateTime);
        var localTime = TimeOnly.FromDateTime(local.DateTime);

        if (_availabilityExceptions.FirstOrDefault(exception => exception.Covers(localDate)) is { } governing)
        {
            return governing.IsOpenAt(localTime);
        }

        var hours = _operatingHours.SingleOrDefault(candidate => candidate.Day == local.DayOfWeek);

        // No schedule recorded for the day is not the same as closed. Returning null lets the
        // client say nothing rather than assert something the provider never told it.
        return hours is null || !hours.IsSpecified ? null : hours.IsOpenAt(localTime);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Verification — projection of Trust
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Records that a verification claim has gone to Trust. Says nothing about the outcome.
    /// </summary>
    public Result SubmitVerification(
        ProviderVerificationKind kind,
        ProviderVerificationId idIfNew,
        Guid trustVerificationId,
        UserId submittedBy,
        DateTimeOffset now,
        ProviderCertificationId? certificationId = null)
    {
        ArgumentNullException.ThrowIfNull(idIfNew);
        ArgumentNullException.ThrowIfNull(submittedBy);

        var claim = FindVerification(kind, certificationId);

        if (claim is null)
        {
            claim = ProviderVerification.Create(idIfNew, kind, certificationId);
            _verifications.Add(claim);
        }
        else if (claim.IsAwaitingDecision)
        {
            return Result.Failure(ProviderErrors.VerificationAlreadyPending);
        }
        else if (claim.IsCurrent(now))
        {
            return Result.Failure(ProviderErrors.VerificationAlreadyGranted);
        }

        var previous = claim.State;
        claim.Submit(trustVerificationId, submittedBy, now);

        if (certificationId is { } certification && FindCertification(certification) is { } record)
        {
            record.MarkSubmitted(trustVerificationId);
        }

        RecomputeVerificationSummary(now);

        RaiseDomainEvent(new ProviderVerificationSubmittedDomainEvent
        {
            ProviderId = Id,
            Kind = kind,
            TrustVerificationId = trustVerificationId,
            SubmittedBy = submittedBy
        });

        RaiseVerificationChanged(kind, previous, claim.State, claim.ExpiresAt, null);

        return Result.Success();
    }

    /// <summary>
    /// Applies an outcome Trust decided.
    /// <para>
    /// The only path to a verified badge, and it is reachable only from a handler for a Trust
    /// integration event. Matched on Trust's own verification identifier where one is supplied,
    /// so an outcome for a claim this module never saw the submission of — after a replay, or a
    /// deployment gap — still lands on the right row.
    /// </para>
    /// </summary>
    public Result ApplyVerificationOutcome(
        ProviderVerificationKind kind,
        ProviderVerificationState outcome,
        DateTimeOffset decidedAt,
        DateTimeOffset? expiresAt = null,
        string? reasonCode = null,
        string? reason = null,
        Guid? trustVerificationId = null,
        ProviderVerificationId? idIfNew = null)
    {
        var claim = trustVerificationId is { } trustId
            ? _verifications.FirstOrDefault(candidate => candidate.TrustVerificationId == trustId)
              ?? FindVerification(kind, null)
            : FindVerification(kind, null);

        if (claim is null)
        {
            if (idIfNew is null)
            {
                return Result.Failure(ProviderErrors.VerificationNotFound);
            }

            claim = ProviderVerification.Create(idIfNew, kind);
            _verifications.Add(claim);
        }

        var previous = claim.State;

        switch (outcome)
        {
            case ProviderVerificationState.Verified:
                claim.Grant(decidedAt, expiresAt);
                break;

            case ProviderVerificationState.Rejected:
                claim.Reject(decidedAt, reasonCode, reason);
                break;

            case ProviderVerificationState.Expired:
                claim.Expire(decidedAt);
                break;

            case ProviderVerificationState.Revoked:
                claim.Revoke(decidedAt, reason);
                break;

            default:
                return Result.Failure(ProviderErrors.EnumUnknown);
        }

        ApplyCertificationOutcome(claim, outcome, decidedAt);
        RecomputeVerificationSummary(decidedAt);
        RaiseVerificationChanged(kind, previous, claim.State, claim.ExpiresAt, reasonCode);

        return Result.Success();
    }

    /// <summary>
    /// Recomputes the denormalised verification rollup, honouring expiry as at now.
    /// <para>
    /// Called after every claim change and by the sweeper, so a grant that lapses overnight
    /// stops showing a badge without waiting for the provider to be edited.
    /// </para>
    /// </summary>
    public void RecomputeVerificationSummary(DateTimeOffset now) =>
        VerificationSummary = ProviderVerificationSummary.From(
            _verifications.Select(claim => (claim.State, claim.DecidedAt, claim.ExpiresAt)),
            now);

    public ProviderVerification? FindVerification(
        ProviderVerificationKind kind, ProviderCertificationId? certificationId) =>
        _verifications.FirstOrDefault(claim =>
            claim.Kind == kind
            && (certificationId is null || claim.CertificationId == certificationId));

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Audience, statistics and rollups
    // ════════════════════════════════════════════════════════════════════════════════════════

    public Result SetVisibility(ProviderVisibility visibility, UserId? changedBy = null)
    {
        ArgumentNullException.ThrowIfNull(visibility);

        if (Visibility.Equals(visibility))
        {
            return Result.Success();
        }

        var previous = Visibility;
        Visibility = visibility;

        RaiseDomainEvent(new ProviderVisibilityChangedDomainEvent
        {
            ProviderId = Id,
            PreviousLevel = previous.Level,
            CurrentLevel = visibility.Level,
            CountryCode = visibility.CountryCode,
            AreaId = visibility.AreaId,
            ChangedBy = changedBy
        });

        return Result.Success();
    }

    public void RecordView() => Statistics = Statistics.WithView();

    public void RecordContact() => Statistics = Statistics.WithContact();

    public void RecordImpressions(long count) => Statistics = Statistics.WithImpressions(count);

    public void ApplyBookmarkDelta(int delta) => Statistics = Statistics.WithBookmarkDelta(delta);

    public void ApplyShareDelta(int delta) => Statistics = Statistics.WithShareDelta(delta);

    public void ApplyFollowerDelta(int delta) => Statistics = Statistics.WithFollowerDelta(delta);

    public void ApplyPostDelta(int delta) => Statistics = Statistics.WithPostDelta(delta);

    public void ApplyRecommendationCount(int count) =>
        Statistics = Statistics.WithRecommendationCount(count);

    /// <summary>
    /// Replaces the rating rollup with one recomputed from the reviews table.
    /// <para>
    /// Replaced rather than adjusted, for the reason <see cref="ProviderReviewSummary"/> spells
    /// out: a summary maintained by increments drifts the first time a review is edited, hidden
    /// or withdrawn, and it drifts silently.
    /// </para>
    /// </summary>
    public void ApplyReviewSummary(ProviderReviewSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        ReviewSummary = summary;
        Statistics = Statistics.WithReviewCount(summary.TotalReviews);
    }

    /// <summary>
    /// Recomputes how complete the profile is.
    /// <para>
    /// Ten equally weighted signals rather than a curve, because the number's job is to tell an
    /// owner what to do next, and an opaque score that moves by different amounts for different
    /// actions does not. Each signal is one thing a member notices is missing.
    /// </para>
    /// </summary>
    public void RecomputeProfileCompletion()
    {
        var signals = new[]
        {
            Profile.Description.Body.Length >= 60,
            Profile.CoverMediaId is not null,
            Profile.LogoMediaId is not null,
            _gallery.Count >= 3,
            _contactMethods.Count > 0,
            !Location.IsEmpty,
            _services.Count > 0,
            _operatingHours.Exists(hours => hours.IsSpecified),
            _languages.Count > 0,
            _tags.Count > 0 || _certifications.Count > 0
        };

        var achieved = signals.Count(signal => signal);

        Statistics = Statistics.WithProfileCompletion(achieved * 100 / signals.Length);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Soft delete & auditing
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Marks the provider deleted.
    /// <para>
    /// Sets the fields directly rather than relying on the change tracker, for the reason B6
    /// records: a delete that happens in the tracker cannot also set a status or raise an event,
    /// and both are needed here. B1.0.1's <c>SoftDeleteConverter</c> repairs the owned-graph
    /// cascade for the tracker path; this path never triggers it.
    /// </para>
    /// </summary>
    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
        Status = ProviderStatus.Archived;
        StatusChangedAt = timestamp;
    }

    /// <summary>
    /// Undoes a soft delete, returning the provider to the state it can meaningfully resume.
    /// Draft when it never published, Active when it did.
    /// </summary>
    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
        Status = PublishedAt is null ? ProviderStatus.Draft : ProviderStatus.Active;
    }

    /// <summary>Stamped by the shared persistence interceptor. Never called by a handler.</summary>
    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    /// <inheritdoc cref="SetCreated" />
    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Internals
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The bar for publication: a name, a category and one way to be reached.
    /// <para>
    /// Deliberately low. A higher bar — photographs, hours, services — would be a better listing
    /// and a worse product, because the provider who cannot clear it simply does not list, and a
    /// directory with fewer real businesses in it helps nobody.
    /// </para>
    /// </summary>
    public bool IsPublishable =>
        Name.Value.Length >= ProviderName.MinLength
        && _contactMethods.Count > 0;

    private bool CanBeEdited =>
        !IsDeleted && Status is not (ProviderStatus.Archived or ProviderStatus.Suspended);

    private int OwnerCount => _members.Count(member => member.Role == ProviderRole.Owner);

    private Result MoveStatus(
        ProviderStatus target,
        DateTimeOffset now,
        string? reason,
        UserId? changedBy,
        bool allowFromSuspended)
    {
        if (IsDeleted)
        {
            return Result.Failure(ProviderErrors.Removed);
        }

        if (!allowFromSuspended && Status == ProviderStatus.Suspended)
        {
            return Result.Failure(ProviderErrors.Suspended);
        }

        if (Status == target)
        {
            return Result.Success();
        }

        var previous = Status;

        Status = target;
        StatusChangedAt = now;
        StatusReason = Truncate(reason, 500);

        RaiseStatusChanged(previous, changedBy, StatusReason);

        return Result.Success();
    }

    private void RaiseStatusChanged(ProviderStatus previous, UserId? changedBy, string? reason = null)
    {
        if (previous == Status)
        {
            return;
        }

        RaiseDomainEvent(new ProviderStatusChangedDomainEvent
        {
            ProviderId = Id,
            PreviousStatus = previous,
            CurrentStatus = Status,
            ChangedBy = changedBy,
            Reason = reason,
            OwnerUserId = NotifiableOwnerId
        });
    }

    private void RaiseUpdated(IReadOnlyList<string> fields, UserId? changedBy) =>
        RaiseDomainEvent(new ProviderUpdatedDomainEvent
        {
            ProviderId = Id,
            ChangedBy = changedBy,
            ChangedFields = fields,
            OwnerUserId = NotifiableOwnerId
        });

    private void RaiseServiceAreasChanged(UserId? changedBy) =>
        RaiseDomainEvent(new ProviderServiceAreasChangedDomainEvent
        {
            ProviderId = Id,
            AreaCount = _serviceAreas.Count,
            ServesRemotely = ServesRemotely,
            ChangedBy = changedBy
        });

    private void RaiseAvailabilityChanged(
        UserId? changedBy, bool affectsToday, DateOnly? effectiveFrom = null) =>
        RaiseDomainEvent(new ProviderAvailabilityChangedDomainEvent
        {
            ProviderId = Id,
            Kind = Availability.Kind,
            AffectsToday = affectsToday,
            EffectiveFrom = effectiveFrom,
            ChangedBy = changedBy
        });

    private void RaiseVerificationChanged(
        ProviderVerificationKind kind,
        ProviderVerificationState previous,
        ProviderVerificationState current,
        DateTimeOffset? expiresAt,
        string? reasonCode)
    {
        if (previous == current)
        {
            return;
        }

        RaiseDomainEvent(new ProviderVerificationChangedDomainEvent
        {
            ProviderId = Id,
            Kind = kind,
            PreviousState = previous,
            CurrentState = current,
            IsVerifiedNow = VerificationSummary.IsVerified,
            ExpiresAt = expiresAt,
            ReasonCode = reasonCode,
            OwnerUserId = NotifiableOwnerId
        });
    }

    private void ApplyCertificationOutcome(
        ProviderVerification claim, ProviderVerificationState outcome, DateTimeOffset decidedAt)
    {
        if (claim.CertificationId is not { } certificationId
            || FindCertification(certificationId) is not { } certification)
        {
            return;
        }

        switch (outcome)
        {
            case ProviderVerificationState.Verified:
                certification.MarkVerified(decidedAt);
                break;

            case ProviderVerificationState.Rejected:
                certification.MarkRejected();
                break;

            case ProviderVerificationState.Expired:
                certification.MarkExpired();
                break;

            case ProviderVerificationState.Revoked:
                certification.MarkRevoked();
                break;

            default:
                break;
        }
    }

    private void ResequenceServices()
    {
        var ordered = _services.OrderBy(service => service.Position).ToList();

        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].SetPosition(index);
        }
    }

    private void ResequenceGallery()
    {
        foreach (var group in _gallery.GroupBy(item => item.Role, StringComparer.Ordinal))
        {
            var ordered = group.OrderBy(item => item.Position).ToList();

            for (var index = 0; index < ordered.Count; index++)
            {
                ordered[index].SetPosition(index);
            }
        }
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, max)];
}
