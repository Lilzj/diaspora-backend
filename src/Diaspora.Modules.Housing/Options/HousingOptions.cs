namespace Diaspora.Modules.Housing.Options;

/// <summary>
/// The knobs an operator can turn.
/// <para>
/// <b>Everything here is an abuse bound, a schedule or a jurisdiction decision, and nothing here is
/// a domain rule.</b> The distinction is the one <c>HousingLimits</c> states from the other side: a
/// bound an aggregate is <em>correct</em> at is a constant in the domain, because an invariant that
/// varies by deployment is not an invariant. What lives here is the set of numbers a deployment
/// genuinely might need to differ on.
/// </para>
/// </summary>
public sealed class HousingOptions
{
    public const string SectionName = "Housing";

    /// <summary>
    /// Live properties one landlord may hold at once.
    /// <para>
    /// Generous, because a letting agency legitimately runs hundreds, and present only so a
    /// scripted client cannot publish forty thousand overnight.
    /// </para>
    /// </summary>
    public int MaxActiveListingsPerLandlord { get; set; } = 300;

    /// <summary>
    /// Open viewings one member may hold at once.
    /// <para>
    /// Ten: more than anybody house-hunting seriously needs in one week, and low enough that a
    /// script cannot arrange to collect the addresses of every empty home in a city.
    /// </para>
    /// </summary>
    public int MaxOpenViewingsPerSeeker { get; set; } = 10;

    /// <summary>
    /// Live reservations one member may hold across the whole platform.
    /// <para>
    /// <b>The most consequential number in this file.</b> Every open reservation is a home nobody
    /// else can take, so it is deliberately small: somebody genuinely choosing between properties
    /// holds two or three, and anybody holding twelve is taking homes off the market.
    /// </para>
    /// </summary>
    public int MaxActiveReservationsPerSeeker { get; set; } = 3;

    /// <summary>Neighbourhood insights one member may submit in a day.</summary>
    public int MaxInsightsPerAuthorPerDay { get; set; } = 10;

    /// <summary>
    /// How long a reservation holds a property when the parties agreed no expiry, in hours.
    /// <para>
    /// Seventy-two hours: long enough for a member to arrange a deposit from another country,
    /// short enough that somebody who has gone quiet does not keep a home off the market for a week.
    /// Longer than B10's forty-eight because a housing decision genuinely takes longer than a
    /// second-hand purchase.
    /// </para>
    /// </summary>
    public int DefaultReservationHoldHours { get; set; } = 72;

    /// <summary>How long a viewing request stands unanswered before the sweep expires it, in hours.</summary>
    public int DefaultViewingRequestHours { get; set; } = 72;

    /// <summary>Default viewing duration in minutes, where the parties did not say.</summary>
    public int DefaultViewingDurationMinutes { get; set; } = 30;

    /// <summary>
    /// How many distinct reports move a listing into the moderation queue automatically.
    /// <para>
    /// Two rather than one, because a single report is an allegation and one-report auto-flagging
    /// is a tool a competing landlord can use. A scam allegation flags on the first regardless —
    /// that rule is in the aggregate, not here, because it is a safety decision rather than a
    /// tuning knob.
    /// </para>
    /// </summary>
    public int ReportsBeforeAutoFlag { get; set; } = 2;

    /// <summary>How long a view is de-duplicated for, in minutes.</summary>
    public int UniqueViewWindowMinutes { get; set; } = 60;

    /// <summary>How many (listing, viewer) pairs the in-process view tracker holds before pruning.</summary>
    public int ViewTrackerCapacity { get; set; } = 100_000;

    /// <summary>How often the availability sweep runs, in minutes.</summary>
    public int AvailabilitySweepIntervalMinutes { get; set; } = 15;

    /// <summary>How often the viewing- and reservation-expiry sweep runs, in minutes.</summary>
    public int CommitmentSweepIntervalMinutes { get; set; } = 10;

    /// <summary>How many rows any one sweep pass may take.</summary>
    public int SweepBatchSize { get; set; } = 200;

    /// <summary>
    /// Whether the module seeds its accommodation-type and amenity catalogues at startup. Off for a
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
    /// human queue rather than by a heuristic holding a landlord's listing.
    /// </para>
    /// </summary>
    public bool EnforceFraudSignals { get; set; } = true;

    /// <summary>
    /// Whether a landlord may state a nationality preference for a roommate.
    /// <para>
    /// <b>False unless an operator deliberately turns it on, and the default is the point.</b> The
    /// brief asks for nationality preference as future policy-configurable, and this is what that
    /// means: a deployment operating where such a preference is lawful and customary can enable it,
    /// and every other deployment silently drops the field rather than refusing the listing. Housing
    /// does not decide which jurisdictions those are, and it will not guess.
    /// </para>
    /// </summary>
    public bool AllowNationalityPreference { get; set; }

    /// <summary>
    /// Whether members may submit neighbourhood insights at all.
    /// <para>
    /// On by default. A deployment launching in a new market with no moderator coverage yet turns
    /// it off rather than accumulating an unreviewed queue of claims about real residential areas.
    /// </para>
    /// </summary>
    public bool EnableCommunityInsights { get; set; } = true;

    /// <summary>
    /// The B8 guides that belong beside a listing, keyed by country code.
    /// <para>
    /// <b>Configuration rather than a cross-module query, and deliberately.</b> B8 has no
    /// country-scoped guide listing; B10 recorded that two verticals had already wanted one and had
    /// each declined to reach into another module's read model to get it. B11 is the third to want
    /// it and declines for the same reason. Until B8 publishes such a contract, an operator names
    /// the guides here and Housing checks each still exists through B8's published directory.
    /// </para>
    /// <para>
    /// The key <c>*</c> holds guides that apply everywhere — the scam warning, the deposit
    /// explainer — and is concatenated with the country list rather than replaced by it.
    /// </para>
    /// </summary>
    public IDictionary<string, IReadOnlyList<Guid>> ContextualGuides { get; } =
        new Dictionary<string, IReadOnlyList<Guid>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// How long a neighbourhood profile may go unreviewed before it is flagged as stale, in days.
    /// <para>
    /// A year. Prose about a place is a living summary rather than a fact, and one nobody has
    /// looked at since the tram line opened is quietly wrong.
    /// </para>
    /// </summary>
    public int NeighborhoodReviewIntervalDays { get; set; } = 365;
}
