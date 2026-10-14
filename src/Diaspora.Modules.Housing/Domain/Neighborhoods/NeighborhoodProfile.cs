using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Events;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Domain.Neighborhoods;

/// <summary>
/// What it is like to live somewhere.
/// <para>
/// <b>This aggregate does not own a neighbourhood. It owns a profile of one.</b> The distinction
/// is the single most important decision in B11 and it is worth stating plainly: B4 already models
/// neighbourhoods, as <c>AdministrativeArea</c> nodes at <c>AdministrativeLevel.Neighborhood</c>,
/// with names, slugs, hierarchy paths, centroids and a hub endpoint. Housing must not create a
/// second Peckham. What Housing adds is everything B4 has no business holding: the relocation
/// brief, the liveability bands, and the moderated things members say about the place.
/// </para>
/// <para>
/// <b>The join key is <see cref="AreaId"/> and it is unique.</b> One profile per area, enforced by
/// the index rather than by a handler remembering. A listing points at the same
/// <c>AreaId</c> through its location context, so "every property in this neighbourhood" and "what
/// this neighbourhood is like" are answered from one identifier that neither answer owns.
/// </para>
/// <para>
/// <b>Why the profile is a Housing aggregate and not a Location one.</b> The content is editorial
/// and moderated: it has authors, approvers, review dates and a community submission queue, none
/// of which belongs in a geographic catalogue whose job is to be an authoritative reference. B4
/// stays a reference; B11 owns an opinion about it. The profile is published to other modules
/// through <c>INeighborhoodDirectory</c>, so Jobs, Marketplace, Providers and Guides can render a
/// neighbourhood without referencing this module's aggregates — which is what "reusable, first
/// class, not duplicated" means in practice.
/// </para>
/// <para>
/// <b>The names are cached, not owned.</b> <see cref="Name"/>, <see cref="CityName"/> and the rest
/// are copies of B4's values so a profile card need not join across modules; B4 wins on every
/// disagreement, and a rename refreshes them on the profile's next write.
/// </para>
/// </summary>
public sealed class NeighborhoodProfile
    : AggregateRoot<NeighborhoodProfileId>, IAuditableEntity, ISoftDeletable
{
    private readonly List<NeighborhoodLanguage> _popularLanguages = [];

    private NeighborhoodProfile(
        NeighborhoodProfileId id,
        Guid areaId,
        string countryCode,
        string name,
        string slug,
        DateTimeOffset createdAt) : base(id)
    {
        AreaId = areaId;
        CountryCode = countryCode;
        Name = name;
        Slug = slug;
        Narrative = NeighborhoodNarrative.Empty;
        LivingIndex = NeighborhoodLivingIndex.Unassessed;
        CreatedAt = createdAt;
    }

    private NeighborhoodProfile() { }

    // ── The place, as B4 knows it ───────────────────────────────────────────────────────────

    /// <summary>
    /// B4's administrative area. The neighbourhood itself, which this module does not own, and the
    /// key everything about the place is joined on.
    /// </summary>
    public Guid AreaId { get; private set; }

    public string CountryCode { get; private set; } = string.Empty;

    /// <summary>Cached from B4 for rendering. B4 owns the truth.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Cached from B4. The client's neighbourhood route already addresses places by slug, so this
    /// is what a link resolves against.
    /// </summary>
    public string Slug { get; private set; } = string.Empty;

    public string? CityName { get; private set; }

    public string? RegionName { get; private set; }

    /// <summary>B4's materialised path, cached so a subtree query needs no join to Location.</summary>
    public string? AreaPath { get; private set; }

    /// <summary>
    /// The neighbourhood's centre, copied from B4 (14 Oct 2026). How a profile is found from a
    /// point once the catalogue is retired: the area around a point lists the profiles whose centre
    /// is inside its circle. The profile keeps its editor's <see cref="Name"/>; the point says only
    /// where that place is.
    /// </summary>
    public double? Latitude { get; private set; }

    public double? Longitude { get; private set; }

    // ── The profile, which Housing owns ─────────────────────────────────────────────────────

    /// <summary>The living summaries. Prose, revised as places change.</summary>
    public NeighborhoodNarrative Narrative { get; private set; } = default!;

    /// <summary>The comparable bands. What a side-by-side comparison renders.</summary>
    public NeighborhoodLivingIndex LivingIndex { get; private set; } = default!;

    /// <summary>
    /// Languages you will hear. Short, and the single most relevant field on the whole profile for
    /// a member deciding whether they will be able to talk to their neighbours.
    /// </summary>
    public IReadOnlyList<NeighborhoodLanguage> PopularLanguages =>
        _popularLanguages.OrderByDescending(language => language.Prominence).ToList().AsReadOnly();

    /// <summary>Whether an approver has signed off the current narrative.</summary>
    public bool IsApproved { get; private set; }

    /// <summary>When the profile was last reviewed. Prose about a place goes stale.</summary>
    public DateTimeOffset? LastReviewedAt { get; private set; }

    public UserId? LastReviewedBy { get; private set; }

    /// <summary>Published insights. Denormalised so a profile card need not count rows.</summary>
    public int ApprovedInsightCount { get; private set; }

    /// <summary>Live housing listings here. Refreshed by the listing lifecycle handler.</summary>
    public int ListingCount { get; private set; }

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

    /// <summary>
    /// How complete the profile is, as a fraction. Seven summaries and nine indexes.
    /// <para>
    /// Rendered as a prompt to contribute rather than as a quality score. A neighbourhood with a
    /// thin profile is one nobody has written about yet, not a bad place to live.
    /// </para>
    /// </summary>
    public double Completeness =>
        (Narrative.WrittenCount + LivingIndex.AssessedCount) / 16d;

    /// <summary>Whether there is enough here to be worth rendering a profile page at all.</summary>
    public bool HasContent =>
        !Narrative.IsEmpty || LivingIndex.Assessed || ApprovedInsightCount > 0;

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Starts a profile for a place B4 already knows about.
    /// <para>
    /// The caller has already resolved the area through Location's catalogue and confirmed it is
    /// at neighbourhood level — this aggregate cannot check another module's hierarchy, and
    /// inventing an area identifier here would be the duplication the whole design refuses.
    /// </para>
    /// </summary>
    public static Result<NeighborhoodProfile> Create(
        NeighborhoodProfileId id,
        Guid areaId,
        string? countryCode,
        string? name,
        string? slug,
        DateTimeOffset createdAt,
        UserId? createdBy = null,
        string? cityName = null,
        string? regionName = null,
        string? areaPath = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (areaId == Guid.Empty)
        {
            return Result.Failure<NeighborhoodProfile>(HousingErrors.NeighborhoodAreaInvalid);
        }

        var country = HousingLocationContext.NormalizeCountry(countryCode);

        if (country is null)
        {
            return Result.Failure<NeighborhoodProfile>(HousingErrors.CountryInvalid);
        }

        var placeName = Clamp(name, HousingLimits.MaxLocationName);
        var placeSlug = HousingSlug.Slugify(slug ?? name);

        if (placeName is null || placeSlug is null)
        {
            return Result.Failure<NeighborhoodProfile>(HousingErrors.NeighborhoodAreaInvalid);
        }

        var profile = new NeighborhoodProfile(
            id, areaId, country, placeName, placeSlug, createdAt)
        {
            CityName = Clamp(cityName, HousingLimits.MaxLocationName),
            RegionName = Clamp(regionName, HousingLimits.MaxLocationName),
            AreaPath = Clamp(areaPath, HousingLimits.MaxAreaPath)
        };

        profile.RaiseDomainEvent(new NeighborhoodProfileCreatedDomainEvent
        {
            ProfileId = id,
            AreaId = areaId,
            CountryCode = country,
            Name = placeName,
            CreatedBy = createdBy
        });

        return Result.Success(profile);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Content
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Replaces the living summaries.
    /// <para>
    /// <b>Writing is not approving.</b> An editor's change lands unapproved and the profile says so
    /// until somebody with the approval permission signs it off. That is what makes these summaries
    /// safe to let a wide set of contributors edit: the platform publishes prose about the places
    /// members live, and unreviewed prose about a real neighbourhood is a liability.
    /// </para>
    /// </summary>
    public Result UpdateNarrative(
        NeighborhoodNarrative narrative,
        UserId? changedBy,
        DateTimeOffset now,
        int? baseRevision = null)
    {
        ArgumentNullException.ThrowIfNull(narrative);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (narrative == Narrative)
        {
            return Result.Success();
        }

        Narrative = narrative;
        IsApproved = false;
        ModifiedAt = now;
        Revision++;

        RaiseSummaryUpdated([nameof(Narrative)], changedBy, isApproved: false);

        return Result.Success();
    }

    /// <summary>Replaces the liveability bands. Unapproves for the same reason the narrative does.</summary>
    public Result UpdateLivingIndex(
        NeighborhoodLivingIndex livingIndex,
        UserId? changedBy,
        DateTimeOffset now,
        int? baseRevision = null)
    {
        ArgumentNullException.ThrowIfNull(livingIndex);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (livingIndex == LivingIndex)
        {
            return Result.Success();
        }

        LivingIndex = livingIndex;
        IsApproved = false;
        ModifiedAt = now;
        Revision++;

        RaiseSummaryUpdated([nameof(LivingIndex)], changedBy, isApproved: false);

        return Result.Success();
    }

    /// <summary>Replaces the popular-languages list wholesale.</summary>
    public Result SetPopularLanguages(
        IReadOnlyList<NeighborhoodLanguage> languages, UserId? changedBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(languages);

        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        var deduplicated = languages
            .Where(language => language.Name.Length > 0)
            .GroupBy(language => language.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (deduplicated.Count > HousingLimits.MaxNeighborhoodLanguages)
        {
            return Result.Failure(HousingErrors.NeighborhoodLanguageLimit);
        }

        _popularLanguages.Clear();
        _popularLanguages.AddRange(deduplicated);

        ModifiedAt = now;
        Revision++;

        RaiseSummaryUpdated([nameof(PopularLanguages)], changedBy, IsApproved);

        return Result.Success();
    }

    /// <summary>
    /// Signs the current profile off.
    /// <para>
    /// A separate act from writing, with its own permission. It also stamps
    /// <see cref="LastReviewedAt"/>, which is what lets the platform find profiles that have not
    /// been looked at for two years and are quietly describing a place that has changed.
    /// </para>
    /// </summary>
    public Result Approve(UserId approvedBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(approvedBy);

        if (IsDeleted)
        {
            return Result.Failure(HousingErrors.NeighborhoodProfileNotFound);
        }

        IsApproved = true;
        LastReviewedAt = now;
        LastReviewedBy = approvedBy;
        ModifiedAt = now;

        RaiseSummaryUpdated([nameof(IsApproved)], approvedBy, isApproved: true);

        return Result.Success();
    }

    /// <summary>
    /// Refreshes the names copied from B4 after a rename or a boundary change.
    /// <para>
    /// Deliberately not a content edit: it raises no summary event and does not unapprove the
    /// profile. A place being renamed says nothing about whether the prose about it is still true.
    /// </para>
    /// </summary>
    public void RefreshPlace(
        string? name, string? slug, string? cityName, string? regionName, string? areaPath)
    {
        if (Clamp(name, HousingLimits.MaxLocationName) is { } placeName)
        {
            Name = placeName;
        }

        if (HousingSlug.Slugify(slug) is { } placeSlug)
        {
            Slug = placeSlug;
        }

        CityName = Clamp(cityName, HousingLimits.MaxLocationName) ?? CityName;
        RegionName = Clamp(regionName, HousingLimits.MaxLocationName) ?? RegionName;
        AreaPath = Clamp(areaPath, HousingLimits.MaxAreaPath) ?? AreaPath;
    }

    /// <summary>
    /// Records where the neighbourhood is. Copied from B4's centre when the profile is created or
    /// its place refreshed; an absent centre leaves the last known point.
    /// </summary>
    public void SetPoint(double? latitude, double? longitude)
    {
        if (latitude is not { } lat || longitude is not { } lng
            || lat is < -90d or > 90d || lng is < -180d or > 180d)
        {
            return;
        }

        Latitude = lat;
        Longitude = lng;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Denormalised counts
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Restates how many published insights this neighbourhood has.
    /// <para>
    /// Set rather than incremented, and that is B1.1's rule for an at-least-once consumer taken as
    /// written: an increment applied twice on a redelivery is a count that drifts upward forever,
    /// and a count nobody can reconcile is worse than no count.
    /// </para>
    /// </summary>
    public void SetApprovedInsightCount(int count) =>
        ApprovedInsightCount = Math.Max(0, count);

    /// <summary>Restates how many live properties are here. Set, not incremented — same reason.</summary>
    public void SetListingCount(int count) => ListingCount = Math.Max(0, count);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Soft delete & auditing
    // ════════════════════════════════════════════════════════════════════════════════════════

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
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

    public Result EnsureRevision(int? baseRevision) =>
        baseRevision is { } expected && expected != Revision
            ? Result.Failure(HousingErrors.RevisionConflict)
            : Result.Success();

    private Result EnsureEditable(int? baseRevision)
    {
        if (IsDeleted)
        {
            return Result.Failure(HousingErrors.NeighborhoodProfileNotFound);
        }

        return EnsureRevision(baseRevision);
    }

    private void RaiseSummaryUpdated(
        IReadOnlyList<string> changedFields, UserId? changedBy, bool isApproved) =>
        RaiseDomainEvent(new NeighborhoodSummaryUpdatedDomainEvent
        {
            ProfileId = Id,
            AreaId = AreaId,
            ChangedFields = changedFields,
            ChangedBy = changedBy,
            IsApproved = isApproved
        });

    private static string? Clamp(string? value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > maximum ? trimmed[..maximum] : trimmed;
    }
}
