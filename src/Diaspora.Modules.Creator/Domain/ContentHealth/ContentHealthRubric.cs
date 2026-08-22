namespace Diaspora.Modules.Creator.Domain.ContentHealth;

/// <summary>
/// What "content health" means on this platform — BG-08.
/// <para>
/// <b>The history this replaces.</b> <c>UnifiedContentItem</c> has carried <c>healthScore</c>,
/// <c>healthStatus</c> and <c>healthRecommendations</c> since the type was written, and every value
/// that ever filled them came from <c>mocks/creator-data.ts</c>. When the fixtures were removed the
/// fields were left undefined and the cards rendered "Health not measured" — honest, and a holding
/// position. BG-08 was the decision behind that sentence. The decision taken was: health is
/// <b>completeness</b>, and nothing else.
/// </para>
///
/// <para>
/// ── <b>Why completeness and not engagement</b> ──
/// </para>
/// <para>
/// A score built from views, saves or enquiries conflates <em>authoring quality</em> with
/// <em>market demand</em>: a well-written listing for an unpopular item would read as unhealthy, and
/// the member could not act on it. Freshness was rejected for a narrower reason — a finished listing
/// would silently decay while its author did nothing wrong. Completeness is the only one of the
/// three that is deterministic, explainable, and <em>actionable</em>: every point lost names a field
/// the member can go and fill in.
/// </para>
///
/// <para>
/// ── <b>Why a rubric rather than a score</b> ──
/// </para>
/// <para>
/// The obvious design is a server-side scorer that reads each member's content and returns numbers.
/// It cannot be built here, and the reason is the same constraint that made BG-10 a defect: the
/// Creator module references Financial and Community and <b>not</b> Guides, Marketplace, Housing or
/// CommunityLife. A scorer that read those would give Creator a dependency on four content modules —
/// precisely the coupling <c>CreatorActivityConsumers</c> exists to avoid, acquired to compute a
/// percentage.
/// </para>
/// <para>
/// So the <b>rule</b> ships from the server and the <b>evaluation</b> happens where the data already
/// is. The client fans out over the per-module <c>/mine</c> routes for the workspace anyway; it now
/// applies this published rubric to what those routes returned. The client does not invent the rule
/// — it cannot, because the rule is here — and changing a weight is a server change every client
/// picks up without a release. That is the same property a server-side scorer would have had, at
/// none of the architectural cost.
/// </para>
/// <para>
/// <b>Versioned</b>, so a client can say which rubric a rendered score came from and two clients on
/// different releases cannot silently disagree about what 80% meant.
/// </para>
/// </summary>
public static class ContentHealthRubric
{
    /// <summary>
    /// Bumped whenever a criterion or a weight changes. A score is only comparable to another score
    /// from the same version.
    /// </summary>
    public const int Version = 1;

    /// <summary>
    /// At or above this, an item is <c>healthy</c>. Below <see cref="NeedsWorkBelow"/> it is
    /// <c>needsWork</c>; between the two it is <c>fair</c>.
    /// </summary>
    public const int HealthyAtOrAbove = 80;

    /// <summary>See <see cref="HealthyAtOrAbove"/>.</summary>
    public const int NeedsWorkBelow = 50;

    /// <summary>
    /// One thing an item either has or has not.
    /// <para>
    /// <see cref="Field"/> is the property name on the item the client already holds, so the
    /// evaluation is a lookup rather than a per-type branch. <see cref="Hint"/> is what the member
    /// is told to do about it — the reason this model was chosen over the alternatives, and
    /// therefore not optional.
    /// </para>
    /// </summary>
    public sealed record Criterion
    {
        public required string Key { get; init; }

        /// <summary>The field on the content item that satisfies it.</summary>
        public required string Field { get; init; }

        /// <summary>Relative weight. Not a percentage — scores are normalised over what applies.</summary>
        public required int Weight { get; init; }

        /// <summary>What the member should do. Rendered verbatim.</summary>
        public required string Hint { get; init; }

        /// <summary>
        /// Content types this applies to. Empty means every type — so an item is never marked down
        /// for lacking a field its own vertical does not have.
        /// </summary>
        public IReadOnlyList<string> AppliesTo { get; init; } = [];
    }

    /// <summary>
    /// The criteria, and the one rule behind which fields are on the list: <b>a field counts if the
    /// owning module's own publish check or search facets already use it.</b> That is what keeps
    /// this from being an opinion — every criterion below is something the platform already treats
    /// as necessary or as a way members find things, rather than something invented here.
    /// </summary>
    public static readonly IReadOnlyList<Criterion> Criteria =
    [
        new()
        {
            Key = "cover",
            Field = "coverImage",
            Weight = 25,
            Hint = "Add a photograph. Members scroll past items they cannot see.",

            // Housing enforces this at publication — HousingListing.IsPublishable requires at least
            // one media reference — and the others rank on it.
        },
        new()
        {
            Key = "summary",
            Field = "summary",
            Weight = 40,
            Hint = "Write a summary. It is what appears in search results.",

            // Was two criteria at 20 each, `summary` and `description`, and that
            // was a defect found on a device: the per-module `/mine` list
            // projections carry a summary and **not** a description, so the
            // second could never be satisfied by a client scoring a list. Every
            // item lost 20 points permanently, and a complete listing rendered
            // "ACTION REQUIRED 18%".
            //
            // A criterion the evaluator cannot see is not a measurement, it is a
            // penalty. Merged rather than dropped, because the thing being
            // measured — did the author write prose about this — is real and is
            // worth two of the other criteria.
        },
        new()
        {
            Key = "category",
            Field = "categoryKey",
            Weight = 15,
            Hint = "Choose a category, so it appears where members look for it.",
        },
        new()
        {
            Key = "price",
            Field = "price",
            Weight = 10,
            Hint = "Say what it costs. Members filter on price, and an undisclosed one is skipped.",
            AppliesTo = ["marketplace", "housing"],
        },
        new()
        {
            Key = "location",
            Field = "location",
            Weight = 10,
            Hint = "Add a city. Most members search their own first.",
            AppliesTo = ["marketplace", "housing", "event", "guide"],
        },
        new()
        {
            Key = "schedule",
            Field = "startsAt",
            Weight = 10,
            Hint = "Set a date and time.",
            AppliesTo = ["event"],
        },
        new()
        {
            Key = "tags",
            Field = "tags",
            Weight = 10,
            Hint = "Add a tag or two. They are how related items find each other.",
        },
    ];
}
