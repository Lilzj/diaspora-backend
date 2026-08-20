namespace Diaspora.Modules.CommunityLife.Persistence.Configurations;

/// <summary>
/// Table names and column widths every Community Life configuration shares.
/// <para>
/// <b>Every table name is prefixed, and not by accident.</b> SQLite — which the integration suite
/// runs on — ignores schemas entirely, so two modules whose tables differ only by schema collide
/// there and nowhere else, which is the worst possible place for a name clash to first appear.
/// <c>events</c>, <c>organizations</c>, <c>members</c>, <c>reports</c> and <c>analytics</c> are all
/// names another module will eventually want, and <c>events</c> is one the platform outbox already
/// half-owns. The prefix is what stops the collision ever happening. B6 through B11 each record the
/// same trap.
/// </para>
/// </summary>
internal static class CommunityLifePersistenceConstants
{
    // ── Organization aggregate ──────────────────────────────────────────────────────────────

    public const string OrganizationsTable = "community_life_organizations";
    public const string OrganizationLeadersTable = "community_life_organization_leaders";
    public const string OrganizationHoursTable = "community_life_organization_hours";
    public const string OrganizationLanguagesTable = "community_life_organization_languages";
    public const string OrganizationServicesTable = "community_life_organization_services";
    public const string OrganizationMediaTable = "community_life_organization_media";
    public const string OrganizationGuidesTable = "community_life_organization_guides";
    public const string OrganizationNotesTable = "community_life_organization_notes";

    // ── Membership aggregate ────────────────────────────────────────────────────────────────

    public const string MembershipsTable = "community_life_memberships";
    public const string MembershipHistoryTable = "community_life_membership_history";

    // ── Event aggregate ─────────────────────────────────────────────────────────────────────

    public const string EventsTable = "community_life_events";
    public const string EventAgendaTable = "community_life_event_agenda";
    public const string EventSpeakersTable = "community_life_event_speakers";
    public const string EventMediaTable = "community_life_event_media";
    public const string EventGuidesTable = "community_life_event_guides";
    public const string EventProvidersTable = "community_life_event_providers";
    public const string EventLinkedResourcesTable = "community_life_event_links";
    public const string EventNotesTable = "community_life_event_notes";

    // ── Series aggregate ────────────────────────────────────────────────────────────────────

    public const string EventSeriesTable = "community_life_event_series";
    public const string EventSeriesExceptionsTable = "community_life_event_series_exceptions";

    // ── RSVP aggregate ──────────────────────────────────────────────────────────────────────

    public const string RsvpsTable = "community_life_rsvps";
    public const string RsvpHistoryTable = "community_life_rsvp_history";

    // ── Catalogues ──────────────────────────────────────────────────────────────────────────

    public const string OrganizationTypesTable = "community_life_organization_types";
    public const string EventCategoriesTable = "community_life_event_categories";

    // ── Safety ──────────────────────────────────────────────────────────────────────────────

    public const string ReportsTable = "community_life_reports";

    // ── Measurement ─────────────────────────────────────────────────────────────────────────

    public const string EventAnalyticsTable = "community_life_event_analytics";
    public const string OrganizationAnalyticsTable = "community_life_organization_analytics";

    // ── Column widths ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Every enum is stored as its name, not its number. Readable in a query window, and safe
    /// against somebody reordering the enum.
    /// </summary>
    public const int EnumLength = 32;

    public const int CountryCodeLength = 2;

    /// <summary>
    /// An IANA time-zone identifier. <c>America/Argentina/ComodRivadavia</c> is the longest in the
    /// database at 32 characters; 64 leaves room for whatever tzdata adds next.
    /// </summary>
    public const int TimeZoneIdLength = 64;

    /// <summary>
    /// A coordinate. Nine total digits with six after the point is roughly a tenth of a metre,
    /// which is far finer than anything this module publishes and exactly what it stores.
    /// </summary>
    public const string CoordinateColumnType = "decimal(9,6)";

    /// <summary>
    /// The comma-separated facet list on an event category. Seven facets at thirty characters is
    /// generous for a set that is read on every browse.
    /// </summary>
    public const int FacetsLength = 256;

    /// <summary>
    /// A recurrence rule's canonical day list — <c>"1,3,5"</c> or <c>"1,15,-1"</c>. Thirty-one
    /// month-days at three characters plus separators fits comfortably; a hundred is generous.
    /// </summary>
    public const int RecurrenceDayListLength = 100;
}
