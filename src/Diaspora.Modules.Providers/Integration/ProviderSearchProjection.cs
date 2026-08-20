namespace Diaspora.Modules.Providers.Integration;

/// <summary>
/// One provider, flattened into the document a search engine would index.
/// <para>
/// <b>B7 builds this and stops.</b> The brief says "Expose provider search projections… Do not
/// implement search", and this record is exactly where that line falls: everything a search
/// engine needs to know about a provider, in one denormalised, serialisable shape, with no
/// tokenising, scoring, faceting or embedding anywhere near it. B15 ingests these and owns
/// everything after.
/// </para>
/// <para>
/// <b>Denormalised on purpose, and heavily.</b> Category names, language names, service names,
/// tags and the geographic breadcrumb are all copied in, because a search index cannot join and
/// because reindexing one document must not require loading six aggregates. That is the trade a
/// projection exists to make.
/// </para>
/// <para>
/// <see cref="Signals"/> is the extension point — see
/// <c>IProviderSearchProjectionContributor</c>. Everything above it is B7's and is frozen;
/// everything in it belongs to whoever put it there.
/// </para>
/// <para>
/// <b>AI readiness.</b> The brief asks for metadata suitable for recommendation, ranking,
/// classification and semantic search without implementing any of it.
/// <see cref="SemanticText"/> is that: one pre-assembled block of everything about the provider
/// that reads as natural language, so an embedding model has a single field to consume and B17
/// does not have to re-derive it from a dozen others.
/// </para>
/// </summary>
public sealed record ProviderSearchProjection
{
    public required Guid ProviderId { get; init; }

    public required string Slug { get; init; }

    public required string Name { get; init; }

    /// <summary>The card-sized precis. Already summarised, so an index need not truncate.</summary>
    public string? Summary { get; init; }

    /// <summary>The full description.</summary>
    public string? Description { get; init; }

    public required string ProviderType { get; init; }

    public required string Status { get; init; }

    public required Guid CategoryId { get; init; }

    public required string CategoryName { get; init; }

    /// <summary>The materialised category path, so a subtree facet is a prefix match.</summary>
    public required string CategoryPath { get; init; }

    /// <summary>Every ancestor category's name, so "Food" retrieves a node named "Restaurants".</summary>
    public IReadOnlyList<string> CategoryAncestors { get; init; } = [];

    /// <summary>Curated retrieval terms from the category. Not names — see the keyword type.</summary>
    public IReadOnlyList<string> CategoryKeywords { get; init; } = [];

    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>BCP-47 codes the provider serves in.</summary>
    public IReadOnlyList<string> Languages { get; init; } = [];

    /// <summary>Service names, so "jollof" retrieves the restaurant that sells it.</summary>
    public IReadOnlyList<string> ServiceNames { get; init; } = [];

    /// <summary>Certification names, so "Gas Safe" retrieves the engineer who holds it.</summary>
    public IReadOnlyList<string> Certifications { get; init; } = [];

    // ── Geography ───────────────────────────────────────────────────────────────────────────

    public string? CountryCode { get; init; }

    public Guid? AreaId { get; init; }

    public string? AreaName { get; init; }

    public string? CityName { get; init; }

    /// <summary>Location's materialised path. A geographic facet is a prefix match on this.</summary>
    public string? HierarchyPath { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>Country codes the provider will serve, beyond where it sits.</summary>
    public IReadOnlyList<string> ServiceAreaCountries { get; init; } = [];

    /// <summary>Area paths the provider will serve. Prefix-matched, like the provider's own.</summary>
    public IReadOnlyList<string> ServiceAreaPaths { get; init; } = [];

    /// <summary>Whether geography should be ignored entirely for this provider.</summary>
    public required bool ServesRemotely { get; init; }

    public required string DeliveryMode { get; init; }

    // ── Quality and trust signals ───────────────────────────────────────────────────────────

    public required bool IsVerified { get; init; }

    /// <summary>Which kinds are currently granted. A facet, and a ranking input.</summary>
    public IReadOnlyList<string> VerifiedKinds { get; init; } = [];

    public required bool IsFeatured { get; init; }

    public required double AverageRating { get; init; }

    public required int TotalReviews { get; init; }

    public required int RecommendationCount { get; init; }

    public required int ProfileCompletionPercent { get; init; }

    public required long ViewCount { get; init; }

    public required long BookmarkCount { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    // ── Extension ───────────────────────────────────────────────────────────────────────────

    /// <summary>Contributed signals, keyed by <c>module.name</c>. B7 neither reads nor validates them.</summary>
    public IReadOnlyDictionary<string, string> Signals { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Everything about this provider that reads as prose, assembled once.
    /// <para>
    /// Exists for the embedding case specifically. An AI module that had to concatenate the name,
    /// the description, the category, the services and the tags itself would be reimplementing
    /// this — and would do it slightly differently from the next module that needed it, so two
    /// embeddings of the same provider would not be comparable.
    /// </para>
    /// </summary>
    public required string SemanticText { get; init; }
}
