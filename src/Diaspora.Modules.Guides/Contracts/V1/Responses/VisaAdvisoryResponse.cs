namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// What the rules are for getting into a country, and who they apply to.
/// <para>
/// <b><see cref="AppliesToCountryCode"/> null means everybody, and a client must render that
/// difference.</b> "Visa-free for 90 days" shown to somebody whose nationality it does not cover is
/// the kind of wrong answer that ends with a person at an airport, so the field is required reading
/// rather than metadata.
/// </para>
/// <para>
/// <b>There is nothing here about applying.</b> No case, no status, no fee, no submission. Application
/// processing is explicitly out of scope, and the contract is where that is visible to every client
/// author who might otherwise assume it is coming.
/// </para>
/// </summary>
public sealed record VisaAdvisoryResponse
{
    public required Guid VisaAdvisoryId { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2 — the destination the rules govern entry to.</summary>
    public required string CountryCode { get; init; }

    /// <summary>The nationality the rules apply to, or null for everybody.</summary>
    public string? AppliesToCountryCode { get; init; }

    /// <summary>Already localized, with the usual fallback.</summary>
    public required string Title { get; init; }

    /// <inheritdoc cref="Title" />
    public required string Summary { get; init; }

    /// <inheritdoc cref="Title" />
    public string? Content { get; init; }

    /// <summary>The visa class — <c>Student</c>, <c>Skilled Worker</c>. Free text; clients group on it.</summary>
    public string? VisaCategory { get; init; }

    /// <summary>PascalCase — <c>Draft</c>, <c>Published</c>, <c>Archived</c>.</summary>
    public required string Status { get; init; }

    // ── Who says so ─────────────────────────────────────────────────────────────────────────

    public required Guid AuthorityId { get; init; }

    public required string AuthorityName { get; init; }

    public required bool IsOfficial { get; init; }

    // ── The rules ───────────────────────────────────────────────────────────────────────────

    /// <summary>Conditions to satisfy, in the order an editor put them. Prose, not structure.</summary>
    public IReadOnlyList<string> Requirements { get; init; } = [];

    /// <summary>Papers to bring, in order.</summary>
    public IReadOnlyList<string> Documents { get; init; } = [];

    /// <summary>How long it takes, as the authority states it — "10 to 15 working days".</summary>
    public string? ProcessingTime { get; init; }

    /// <summary>Where somebody applies. This platform does not.</summary>
    public string? ApplicationUrl { get; init; }

    public string? OfficialReference { get; init; }

    public string? SourceUrl { get; init; }

    // ── When ────────────────────────────────────────────────────────────────────────────────

    public DateTimeOffset? EffectiveFrom { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public DateTimeOffset? LastReviewedAt { get; init; }

    /// <summary>Whether these are the rules that apply right now. Computed so two clients agree.</summary>
    public required bool IsActive { get; init; }

    /// <summary>Guides that explain how to actually do it. Identifiers; the client resolves them.</summary>
    public IReadOnlyList<Guid> RelatedGuideIds { get; init; } = [];

    /// <summary>The locales a translation exists for.</summary>
    public IReadOnlyList<string> AvailableLocales { get; init; } = [];

    /// <summary>The concurrency token to send back on an edit. Editors only.</summary>
    public int? Revision { get; init; }
}
