using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Analytics;

/// <summary>
/// The measured record kept for one event.
/// <para>
/// <b>Separate from the aggregate, and it outlives the event's visibility.</b> An archived or
/// deleted event's figures still belong in an organizer's history and in the platform's category
/// statistics, and keeping them on a soft-deletable aggregate would hide them behind the global
/// query filter the moment somebody tidied up.
/// </para>
/// <para>
/// <b>Every number the brief asks for is here and each is a counter rather than a query.</b> Views,
/// RSVPs, attendance, no-show rate, organizer performance and community participation are all read
/// on dashboards that render dozens of rows, and computing them from the RSVP table would make each
/// dashboard a fan of aggregate scans.
/// </para>
/// </summary>
public sealed class EventAnalytics : Entity<EventAnalyticsId>, IAuditableEntity
{
    private EventAnalytics(
        EventAnalyticsId id,
        CommunityEventId eventId,
        OrganizationId? organizationId,
        string countryCode,
        string categoryKey,
        Guid? neighborhoodAreaId,
        DateTimeOffset openedAt) : base(id)
    {
        EventId = eventId;
        OrganizationId = organizationId;
        CountryCode = countryCode;
        CategoryKey = categoryKey;
        NeighborhoodAreaId = neighborhoodAreaId;
        OpenedAt = openedAt;
        CreatedAt = openedAt;
    }

    private EventAnalytics() { }

    public CommunityEventId EventId { get; private set; } = default!;

    public OrganizationId? OrganizationId { get; private set; }

    /// <summary>Copied at creation so a category rollup is one indexed scan of this table alone.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    public string CategoryKey { get; private set; } = string.Empty;

    public Guid? NeighborhoodAreaId { get; private set; }

    public DateTimeOffset OpenedAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    // ── The funnel ──────────────────────────────────────────────────────────────────────────

    public int ViewCount { get; private set; }

    public int UniqueViewCount { get; private set; }

    public int SaveCount { get; private set; }

    public int ShareCount { get; private set; }

    /// <summary>Every response ever made, including withdrawn ones.</summary>
    public int RsvpCount { get; private set; }

    public int InterestedCount { get; private set; }

    public int GoingCount { get; private set; }

    public int WaitlistedCount { get; private set; }

    public int CancelledCount { get; private set; }

    public int CheckedInCount { get; private set; }

    public int AttendedCount { get; private set; }

    public int NoShowCount { get; private set; }

    public int ReportCount { get; private set; }

    /// <summary>
    /// How long between publication and the event filling, when it did.
    /// <para>
    /// Null on the majority of events, which never fill. That is the honest representation: zero
    /// would say "filled instantly", which is the opposite of what happened.
    /// </para>
    /// </summary>
    public TimeSpan? TimeToFill { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    // ── Derived ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Views that became a response. Null before anybody looked.</summary>
    public double? ConversionRate =>
        UniqueViewCount == 0 ? null : (double)RsvpCount / UniqueViewCount;

    /// <summary>
    /// Commitments that turned up. The number the brief names, and null until the event is over —
    /// a no-show rate computed while the doors are still open is meaningless.
    /// </summary>
    public double? NoShowRate =>
        CompletedAt is null || GoingCount + NoShowCount == 0
            ? null
            : (double)NoShowCount / (GoingCount + NoShowCount);

    /// <summary>Commitments that became attendances.</summary>
    public double? AttendanceRate =>
        CompletedAt is null || GoingCount == 0
            ? null
            : (double)AttendedCount / GoingCount;

    public static EventAnalytics Open(
        EventAnalyticsId id,
        CommunityEventId eventId,
        OrganizationId? organizationId,
        string countryCode,
        string categoryKey,
        Guid? neighborhoodAreaId,
        DateTimeOffset openedAt) =>
        new(id, eventId, organizationId, countryCode, categoryKey, neighborhoodAreaId, openedAt);

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

    public void RecordSave(DateTimeOffset at)
    {
        SaveCount++;
        ModifiedAt = at;
    }

    public void RecordShare(DateTimeOffset at)
    {
        ShareCount++;
        ModifiedAt = at;
    }

    /// <summary>Applies a movement across the response counters. Settled from the RSVP handler.</summary>
    public void RecordResponse(
        DateTimeOffset at,
        int rsvps = 0,
        int interested = 0,
        int going = 0,
        int waitlisted = 0,
        int cancelled = 0,
        int checkedIn = 0,
        int attended = 0,
        int noShow = 0)
    {
        RsvpCount = Math.Max(0, RsvpCount + rsvps);
        InterestedCount = Math.Max(0, InterestedCount + interested);
        GoingCount = Math.Max(0, GoingCount + going);
        WaitlistedCount = Math.Max(0, WaitlistedCount + waitlisted);
        CancelledCount = Math.Max(0, CancelledCount + cancelled);
        CheckedInCount = Math.Max(0, CheckedInCount + checkedIn);
        AttendedCount = Math.Max(0, AttendedCount + attended);
        NoShowCount = Math.Max(0, NoShowCount + noShow);
        ModifiedAt = at;
    }

    public void RecordReport(DateTimeOffset at)
    {
        ReportCount++;
        ModifiedAt = at;
    }

    /// <summary>Stamps the moment the event filled. Recorded once and never moved.</summary>
    public void RecordFilled(DateTimeOffset at)
    {
        if (TimeToFill is not null || PublishedAt is not { } published)
        {
            return;
        }

        TimeToFill = at - published;
        ModifiedAt = at;
    }

    public void RecordCompletion(DateTimeOffset at)
    {
        CompletedAt ??= at;
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
