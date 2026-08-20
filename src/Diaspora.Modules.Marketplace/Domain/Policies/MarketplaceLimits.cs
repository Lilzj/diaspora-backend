namespace Diaspora.Modules.Marketplace.Domain.Policies;

/// <summary>
/// The bounds the aggregates are correct at.
/// <para>
/// <b>Constants rather than options, and the distinction is the one <c>MarketplaceOptions</c>
/// states from the other side.</b> A bound an aggregate enforces to stay <em>correct</em> is an
/// invariant, and an invariant that varies by deployment is not an invariant — a listing with
/// forty photographs on one deployment and twelve on another is two different contracts with the
/// same wire shape. What belongs in options is the abuse ceilings and the sweep schedules, which
/// genuinely do differ.
/// </para>
/// </summary>
public static class MarketplaceLimits
{
    // ── Listing content ─────────────────────────────────────────────────────────────────────

    public const int MaxTitle = 140;

    public const int MinTitle = 3;

    public const int MaxSummary = 400;

    public const int MaxDescription = 20_000;

    public const int MaxSlug = 160;

    public const int MaxCategoryKey = 80;

    /// <summary>How deep the category tree may nest. Three: department, category, sub-category.</summary>
    public const int MaxCategoryDepth = 3;

    public const int MaxDisplayName = 200;

    /// <summary>A short free-text line: a caption, a note, a fulfilment remark.</summary>
    public const int MaxNoteLength = 1_000;

    public const int MaxModerationNote = 2_000;

    public const int MaxModerationNotes = 100;

    public const int MaxTagLength = 40;

    public const int MaxTags = 20;

    // ── Attributes ──────────────────────────────────────────────────────────────────────────

    public const int MaxAttributeKey = 60;

    public const int MaxAttributeValue = 500;

    /// <summary>Attributes on one listing. Generous: a car legitimately carries twenty.</summary>
    public const int MaxAttributes = 40;

    /// <summary>Choices on one attribute definition.</summary>
    public const int MaxAttributeChoices = 100;

    // ── Media ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Media references on one listing. Twenty-four photographs is more than any honest listing
    /// needs and less than a scripted client can use to fill storage.
    /// </summary>
    public const int MaxMediaReferences = 24;

    public const int MaxProviderReferences = 3;

    public const int MaxGuideReferences = 5;

    // ── Money ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The ceiling on any single figure. High enough for a property or a vehicle in a currency
    /// with very small units, low enough that a fat-fingered extra zero is refused.
    /// </summary>
    public const decimal MaxPrice = 999_999_999_999.99m;

    public const int MaxCurrencyCodeLength = 3;

    // ── Inventory ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Units on one listing. A shop with more than this is running a catalogue, not a listing,
    /// and should list per variant.
    /// </summary>
    public const int MaxQuantity = 100_000;

    // ── Negotiation ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Moves in one negotiation before it must be settled or abandoned.
    /// <para>
    /// Twelve is six each. Past that the two are not converging, and an unbounded history is a
    /// row a scripted client can grow without limit against a seller who cannot stop it.
    /// </para>
    /// </summary>
    public const int MaxNegotiationRounds = 12;

    public const int MaxOfferMessage = 1_000;

    // ── Reservations ────────────────────────────────────────────────────────────────────────

    public const int MaxReservationHistory = 50;

    public const int MaxReservationNote = 1_000;

    // ── Reporting ───────────────────────────────────────────────────────────────────────────

    public const int MaxReportNote = 2_000;

    // ── Seller ──────────────────────────────────────────────────────────────────────────────

    public const int MaxSellerName = 200;

    public const int MaxSellerBio = 2_000;

    public const int MaxRegistrationNumber = 100;

    public const int MaxContactValue = 320;

    // ── Location ────────────────────────────────────────────────────────────────────────────

    public const int MaxLocationName = 200;

    public const int MaxAreaPath = 500;

    /// <summary>How far a seller may claim to deliver. Beyond this it is shipping.</summary>
    public const int MaxDeliveryRadiusKm = 500;

    // ── Paging ──────────────────────────────────────────────────────────────────────────────

    /// <summary>The hard ceiling on a page, whatever the caller asks for.</summary>
    public const int MaxPageSize = 100;

    public const int DefaultPageSize = 20;
}
