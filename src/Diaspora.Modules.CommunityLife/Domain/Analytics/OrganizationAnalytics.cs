using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Analytics;

/// <summary>
/// The measured record kept for one organization.
/// <para>
/// Separate from <see cref="EventAnalytics"/> because the two are read at different cadences and by
/// different people: an event's figures are looked at once, the week after; an organization's are a
/// running record of whether the institution is growing. Folding them together would mean every
/// membership-growth query scanned every event ever run.
/// </para>
/// </summary>
public sealed class OrganizationAnalytics : Entity<OrganizationAnalyticsId>, IAuditableEntity
{
    private OrganizationAnalytics(
        OrganizationAnalyticsId id,
        OrganizationId organizationId,
        string countryCode,
        string organizationTypeKey,
        Guid? neighborhoodAreaId,
        DateTimeOffset openedAt) : base(id)
    {
        OrganizationId = organizationId;
        CountryCode = countryCode;
        OrganizationTypeKey = organizationTypeKey;
        NeighborhoodAreaId = neighborhoodAreaId;
        OpenedAt = openedAt;
        CreatedAt = openedAt;
    }

    private OrganizationAnalytics() { }

    public OrganizationId OrganizationId { get; private set; } = default!;

    public string CountryCode { get; private set; } = string.Empty;

    public string OrganizationTypeKey { get; private set; } = string.Empty;

    public Guid? NeighborhoodAreaId { get; private set; }

    public DateTimeOffset OpenedAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    // ── Reach ───────────────────────────────────────────────────────────────────────────────

    public int ViewCount { get; private set; }

    public int UniqueViewCount { get; private set; }

    // ── Membership growth ───────────────────────────────────────────────────────────────────

    /// <summary>Every member ever admitted, including those who have since left.</summary>
    public int TotalJoins { get; private set; }

    public int TotalDepartures { get; private set; }

    public int TotalRemovals { get; private set; }

    public int TotalRejections { get; private set; }

    /// <summary>Members right now. The number that goes on the card.</summary>
    public int CurrentMemberCount { get; private set; }

    /// <summary>The largest it has ever been. What growth is measured against.</summary>
    public int PeakMemberCount { get; private set; }

    // ── Activity ────────────────────────────────────────────────────────────────────────────

    public int EventsPublished { get; private set; }

    public int EventsCompleted { get; private set; }

    public int EventsCancelled { get; private set; }

    public int TotalRsvps { get; private set; }

    public int TotalAttendances { get; private set; }

    public int TotalNoShows { get; private set; }

    /// <summary>
    /// Attendances by members holding <c>Volunteer</c>. The volunteer-activity figure the brief
    /// names, and the one an administrator uses to know who to thank.
    /// </summary>
    public int VolunteerAttendances { get; private set; }

    public int ReportCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    // ── Derived ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Members retained out of everybody ever admitted. Null before anybody joined.
    /// <para>
    /// Rendered to administrators only. It is a number that reads badly out of context — every
    /// student association loses its members on graduation — and a directory that sorted on it
    /// would rank institutions by how little their communities move.
    /// </para>
    /// </summary>
    public double? RetentionRate =>
        TotalJoins == 0 ? null : (double)CurrentMemberCount / TotalJoins;

    /// <summary>Commitments that turned up across every event. The organizer-performance figure.</summary>
    public double? AttendanceRate =>
        TotalAttendances + TotalNoShows == 0
            ? null
            : (double)TotalAttendances / (TotalAttendances + TotalNoShows);

    public static OrganizationAnalytics Open(
        OrganizationAnalyticsId id,
        OrganizationId organizationId,
        string countryCode,
        string organizationTypeKey,
        Guid? neighborhoodAreaId,
        DateTimeOffset openedAt) =>
        new(id, organizationId, countryCode, organizationTypeKey, neighborhoodAreaId, openedAt);

    public void RecordPublication(DateTimeOffset at)
    {
        PublishedAt ??= at;
        ModifiedAt = at;
    }

    public void RecordView(bool isUnique, DateTimeOffset at)
    {
        ViewCount++;

        if (isUnique)
        {
            UniqueViewCount++;
        }

        ModifiedAt = at;
    }

    /// <summary>Applies a movement across the membership counters.</summary>
    public void RecordMembership(
        DateTimeOffset at,
        int joins = 0,
        int departures = 0,
        int removals = 0,
        int rejections = 0,
        int currentDelta = 0)
    {
        TotalJoins = Math.Max(0, TotalJoins + joins);
        TotalDepartures = Math.Max(0, TotalDepartures + departures);
        TotalRemovals = Math.Max(0, TotalRemovals + removals);
        TotalRejections = Math.Max(0, TotalRejections + rejections);
        CurrentMemberCount = Math.Max(0, CurrentMemberCount + currentDelta);

        if (CurrentMemberCount > PeakMemberCount)
        {
            PeakMemberCount = CurrentMemberCount;
        }

        ModifiedAt = at;
    }

    /// <summary>Applies a movement across the event and attendance counters.</summary>
    public void RecordActivity(
        DateTimeOffset at,
        int published = 0,
        int completed = 0,
        int cancelled = 0,
        int rsvps = 0,
        int attendances = 0,
        int noShows = 0,
        int volunteerAttendances = 0)
    {
        EventsPublished = Math.Max(0, EventsPublished + published);
        EventsCompleted = Math.Max(0, EventsCompleted + completed);
        EventsCancelled = Math.Max(0, EventsCancelled + cancelled);
        TotalRsvps = Math.Max(0, TotalRsvps + rsvps);
        TotalAttendances = Math.Max(0, TotalAttendances + attendances);
        TotalNoShows = Math.Max(0, TotalNoShows + noShows);
        VolunteerAttendances = Math.Max(0, VolunteerAttendances + volunteerAttendances);
        ModifiedAt = at;
    }

    public void RecordReport(DateTimeOffset at)
    {
        ReportCount++;
        ModifiedAt = at;
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
}
