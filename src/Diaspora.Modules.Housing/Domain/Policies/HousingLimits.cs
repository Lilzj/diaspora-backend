namespace Diaspora.Modules.Housing.Domain.Policies;

/// <summary>
/// The bounds the aggregates are correct at.
/// <para>
/// <b>Constants rather than options, and the distinction is the one <c>HousingOptions</c> states
/// from the other side.</b> A bound an aggregate enforces to stay <em>correct</em> is an
/// invariant, and an invariant that varies by deployment is not an invariant — a listing with
/// forty photographs on one deployment and twelve on another is two different contracts with the
/// same wire shape. What belongs in options is the abuse ceilings and the sweep schedules, which
/// genuinely do differ. B10 records the same split and B11 keeps it.
/// </para>
/// </summary>
public static class HousingLimits
{
    // ── Listing content ─────────────────────────────────────────────────────────────────────

    public const int MinTitle = 3;

    public const int MaxTitle = 140;

    public const int MaxSummary = 400;

    public const int MaxDescription = 20_000;

    public const int MaxSlug = 160;

    public const int MaxTypeKey = 80;

    public const int MaxAmenityKey = 80;

    public const int MaxDisplayName = 200;

    /// <summary>A short free-text line: a caption, a note, a viewing remark.</summary>
    public const int MaxNoteLength = 1_000;

    public const int MaxModerationNote = 2_000;

    public const int MaxModerationNotes = 100;

    public const int MaxLocationName = 200;

    /// <summary>B4's materialised path, stored verbatim so scope narrowing is an indexed prefix.</summary>
    public const int MaxAreaPath = 512;

    // ── Address ─────────────────────────────────────────────────────────────────────────────

    public const int MaxAddressLine = 200;

    public const int MaxPostalCode = 16;

    // ── Amenities & rules ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Amenities on one property. Forty covers a serviced apartment listing every fixture it has,
    /// and stops a scripted client using the join table as free storage.
    /// </summary>
    public const int MaxAmenities = 40;

    public const int MaxHouseRules = 25;

    public const int MaxHouseRuleLength = 300;

    // ── Media ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Media references on one property. Higher than B10's twenty-four because a home genuinely
    /// has more rooms than a second-hand bicycle has angles, and a floor plan, a video and a
    /// virtual tour all sit in the same collection.
    /// </summary>
    public const int MaxMediaReferences = 40;

    public const int MaxProviderReferences = 3;

    public const int MaxGuideReferences = 8;

    // ── Money ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The largest rent or deposit the module will store. Ten million in any currency is far
    /// above any residential figure and far below the point at which the decimal column overflows.
    /// </summary>
    public const decimal MaxAmount = 10_000_000m;

    public const int CurrencyCodeLength = 3;

    // ── Capacity ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Bedrooms. Zero is legitimate and common — a studio has none — so the floor is zero rather
    /// than one, and a filter for "at least one bedroom" excludes studios correctly by accident.
    /// </summary>
    public const int MaxBedrooms = 50;

    public const int MaxBathrooms = 50;

    public const int MaxOccupants = 200;

    /// <summary>Square metres. Large enough for a whole guest house.</summary>
    public const int MaxFloorArea = 100_000;

    // ── Units ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Identical units one listing may advertise — hostel beds, or flats in one block let on one
    /// advert. One is the overwhelmingly common case and the default.
    /// </summary>
    public const int MaxUnits = 500;

    // ── Stay length ─────────────────────────────────────────────────────────────────────────

    /// <summary>Ten years in nights. A tenancy longer than this is not a listing.</summary>
    public const int MaxStayNights = 3_650;

    // ── Roommate matching ───────────────────────────────────────────────────────────────────

    public const int MaxRoommateLanguages = 10;

    public const int MinRoommateAge = 16;

    public const int MaxRoommateAge = 120;

    // ── Viewings & reservations ─────────────────────────────────────────────────────────────

    public const int MaxViewingNote = 1_000;

    public const int MaxReservationNote = 1_000;

    /// <summary>Entries in one viewing's or one reservation's history. A bound on an append-only list.</summary>
    public const int MaxHistoryEntries = 200;

    // ── Neighbourhood ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// One of the neighbourhood profile's prose summaries. Six of these hang off one profile, so
    /// the bound is per-field rather than per-profile.
    /// </summary>
    public const int MaxNeighborhoodSummary = 4_000;

    public const int MaxNeighborhoodOverview = 8_000;

    public const int MaxNeighborhoodLanguages = 15;

    public const int MaxLanguageName = 60;

    /// <summary>One community insight. Deliberately short: an observation, not an essay.</summary>
    public const int MaxInsightBody = 400;

    public const int MaxInsightLabel = 80;

    /// <summary>The longest Google place name an insight at a point keeps (14 Oct 2026).</summary>
    public const int MaxInsightPlaceName = 200;

    /// <summary>The longest Google place identifier an insight at a point keeps.</summary>
    public const int MaxInsightPlaceId = 256;

    public const int MaxInsightDecisionNote = 1_000;

    // ── Reports ─────────────────────────────────────────────────────────────────────────────

    public const int MaxReportDetail = 2_000;

    public const int MaxResolutionNote = 2_000;

    // ── Paging ──────────────────────────────────────────────────────────────────────────────

    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 100;
}
