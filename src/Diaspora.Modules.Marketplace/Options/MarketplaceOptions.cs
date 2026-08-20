namespace Diaspora.Modules.Marketplace.Options;

/// <summary>
/// The knobs an operator can turn.
/// <para>
/// <b>Everything here is an abuse bound or a schedule, and nothing here is a domain rule.</b> The
/// distinction is the one <c>MarketplaceLimits</c> states from the other side: a bound an aggregate
/// is <em>correct</em> at is a constant in the domain, because an invariant that varies by
/// deployment is not an invariant. What lives here is the set of numbers a deployment genuinely
/// might need to differ on — how many listings one seller may have live, how long a reservation
/// holds stock, how often the sweeps run.
/// </para>
/// </summary>
public sealed class MarketplaceOptions
{
    public const string SectionName = "Marketplace";

    /// <summary>
    /// Live listings one seller may hold at once.
    /// <para>
    /// Generous, because a shop legitimately runs hundreds, and present only so a scripted client
    /// cannot publish forty thousand overnight.
    /// </para>
    /// </summary>
    public int MaxActiveListingsPerSeller { get; set; } = 500;

    /// <summary>
    /// Offers one member may make in a day.
    /// <para>
    /// A bound on scripted lowballing, not on enthusiasm. Set well above what any real shopping
    /// session produces.
    /// </para>
    /// </summary>
    public int MaxOffersPerBuyerPerDay { get; set; } = 60;

    /// <summary>
    /// Live reservations one member may hold across the whole marketplace.
    /// <para>
    /// <b>The most consequential number in this file.</b> Every open reservation is stock nobody
    /// else can buy, so a member who could hold five hundred could empty a category for as long as
    /// the windows lasted.
    /// </para>
    /// </summary>
    public int MaxActiveReservationsPerBuyer { get; set; } = 10;

    /// <summary>Listings one member may keep in their saved list.</summary>
    public int MaxSavedListingsPerMember { get; set; } = 500;

    /// <summary>
    /// How long a reservation holds stock when the parties agreed no window, in hours.
    /// <para>
    /// Forty-eight hours: long enough for a weekend hand-off, short enough that a buyer who has
    /// gone quiet does not keep an item off the market for a week.
    /// </para>
    /// </summary>
    public int DefaultReservationHours { get; set; } = 48;

    /// <summary>How long an offer stands when the buyer set no expiry, in hours.</summary>
    public int DefaultOfferHours { get; set; } = 72;

    /// <summary>
    /// How long a published listing runs before it expires, in days. Zero leaves listings up until
    /// the seller acts, which is what a shop wants and what a one-off seller usually forgets.
    /// </summary>
    public int DefaultListingLifetimeDays { get; set; } = 60;

    /// <summary>
    /// How many distinct reports move a listing into the moderation queue automatically.
    /// <para>
    /// Two rather than one, because a single report is an allegation and one-report auto-flagging
    /// is a tool a competitor can use. Fraud reports flag on the first regardless — that rule is in
    /// the handler, not here, because it is a safety decision rather than a tuning knob.
    /// </para>
    /// </summary>
    public int ReportsBeforeAutoFlag { get; set; } = 2;

    /// <summary>How long a view is de-duplicated for, in minutes. The unique-view window.</summary>
    public int UniqueViewWindowMinutes { get; set; } = 60;

    /// <summary>How many (listing, viewer) pairs the in-process view tracker holds before it prunes.</summary>
    public int ViewTrackerCapacity { get; set; } = 100_000;

    /// <summary>How often the listing-expiry sweep runs, in minutes.</summary>
    public int ExpirySweepIntervalMinutes { get; set; } = 15;

    /// <summary>How many rows any one sweep pass may take.</summary>
    public int SweepBatchSize { get; set; } = 200;

    /// <summary>How often the offer- and reservation-expiry sweep runs, in minutes.</summary>
    public int CommitmentSweepIntervalMinutes { get; set; } = 10;

    /// <summary>How often the promotion sweep runs, in minutes.</summary>
    public int PromotionSweepIntervalMinutes { get; set; } = 30;

    /// <summary>
    /// Whether the module seeds its category and attribute catalogues at startup. Off for a
    /// deployment whose migrations and seed data are applied by a separate job.
    /// </summary>
    public bool SeedCatalogues { get; set; } = true;

    /// <summary>
    /// Whether the background sweeps run in this process. Off for a web instance in a deployment
    /// that runs them in a dedicated worker.
    /// </summary>
    public bool EnableBackgroundSweeps { get; set; } = true;

    /// <summary>
    /// Whether a listing must clear the fraud providers before it publishes.
    /// <para>
    /// On by default. Off is for a deployment that wants signals recorded and decisions made by a
    /// human queue rather than by a heuristic holding a member's listing.
    /// </para>
    /// </summary>
    public bool EnforceFraudSignals { get; set; } = true;
}
