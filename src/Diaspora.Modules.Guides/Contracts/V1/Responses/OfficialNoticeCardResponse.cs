namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One official notice as a card — what a list, a dashboard band and a search hit all render.
/// <para>
/// <b>Shaped to the React Native app's existing <c>UpdateSummary</c> card rather than invented.</b>
/// That component already renders a title, a description, a category, an urgency level, a published
/// and an expiry instant, a verification state and a status, and members already read it as "an
/// important thing about where I live". Reusing its shape means the Explore Diaspora country page can
/// render official notices through a card it already ships, and the additive fields here — the
/// issuing authority and the official flag — are what visually separate a government circular from a
/// neighbour's traffic report.
/// </para>
/// <para>
/// <b>The full body is deliberately absent.</b> A notice can run to forty thousand characters, and a
/// list of twenty of them would be a response nobody renders and everybody pays for. The detail
/// endpoint carries it.
/// </para>
/// </summary>
public sealed record OfficialNoticeCardResponse
{
    public required Guid OfficialNoticeId { get; init; }

    /// <summary>Already localized to the caller's locale, with the usual fallback.</summary>
    public required string Title { get; init; }

    /// <inheritdoc cref="Title" />
    public required string Summary { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    /// <summary>The sub-national area, when the notice does not apply nationwide.</summary>
    public string? Region { get; init; }

    /// <summary>The notice category key. What a client switches on for an icon.</summary>
    public required string CategoryKey { get; init; }

    /// <summary>The category's localized label, so a card needs no second call to render.</summary>
    public required string CategoryName { get; init; }

    public string? CategoryIcon { get; init; }

    /// <summary>PascalCase — <c>Critical</c>, <c>High</c>. Matches the app's existing unions.</summary>
    public required string Severity { get; init; }

    /// <inheritdoc cref="Severity" />
    public required string Status { get; init; }

    /// <inheritdoc cref="Severity" />
    public required string VerificationStatus { get; init; }

    // ── Who says so ─────────────────────────────────────────────────────────────────────────

    public required Guid AuthorityId { get; init; }

    /// <summary>The issuing body's name, resolved in the same batched read as the card.</summary>
    public required string AuthorityName { get; init; }

    /// <summary>The acronym, when the body has one. Rendered where space is short.</summary>
    public string? AuthorityShortName { get; init; }

    /// <inheritdoc cref="Severity" />
    public required string AuthorityType { get; init; }

    /// <summary>
    /// Whether Trust has confirmed the issuing body.
    /// <para>
    /// <b>The single most important boolean on this card.</b> It is what a badge renders, and it is
    /// the difference between "the immigration department announced this" and "somebody typed this
    /// in". It is always true on a published notice — publication requires it — and is carried anyway
    /// so a client rendering an editor's draft list can show which rows are not yet publishable.
    /// </para>
    /// </summary>
    public required bool IsOfficial { get; init; }

    /// <summary>
    /// Written by a Diaspora platform admin. Clients show "Posted by Diaspora platform admin"
    /// and never an official badge (<c>IsOfficial</c> is false for these).
    /// </summary>
    public bool PostedByPlatform { get; init; }

    // ── When ────────────────────────────────────────────────────────────────────────────────

    /// <summary>When the authority issued it, which is not when this platform published it.</summary>
    public DateTimeOffset? PublicationDate { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public DateTimeOffset? EffectiveFrom { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Whether this is guidance right now. Computed server-side so two clients agree.</summary>
    public required bool IsActive { get; init; }

    /// <summary>Whether the expiry has passed, whatever the status says.</summary>
    public required bool IsExpired { get; init; }

    /// <summary>The notice that replaced this one, when it has been superseded.</summary>
    public Guid? SupersededById { get; init; }

    /// <summary>When somebody last confirmed it is still true. The ageing signal.</summary>
    public DateTimeOffset? LastReviewedAt { get; init; }

    // ── Engagement ──────────────────────────────────────────────────────────────────────────

    public required long ViewCount { get; init; }

    /// <summary>How many related items it points at. The card renders a count, not the items.</summary>
    public required int ReferenceCount { get; init; }

    /// <summary>How many media attachments it carries.</summary>
    public required int AttachmentCount { get; init; }
}
