using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Policies;

namespace Diaspora.Modules.Guides.Domain.Intelligence;

/// <summary>
/// Everything a query can ask the official notice table for.
/// <para>
/// A filter record rather than an <c>IQueryable</c>, for the reason B7's and B8's equivalents both
/// document: a contributed search engine composes a filter and hands it back; it never composes an
/// expression tree, never touches a <c>DbContext</c>, and cannot write a predicate EF fails to
/// translate at run time inside somebody else's stack trace. Every field here is one the repository
/// knows how to translate — a promise the repository can actually keep.
/// </para>
/// <para>
/// <b>Visibility is computed before the query, never inside it.</b> <see cref="ViewerIsEditor"/>
/// tells the repository whether drafts and submissions may appear, and the repository turns that into
/// predicates. No caller composes its own, which is what stops the fourth listing screen from
/// inventing a fifth definition of what a reader can see.
/// </para>
/// </summary>
public sealed record NoticeQueryFilter
{
    /// <summary>
    /// Which statuses count.
    /// <para>
    /// Published <em>and</em> superseded by default, which is not an oversight: a superseded notice
    /// is still the correct answer to "what did they say in March", and every reader-facing listing
    /// that wants only current guidance sets <see cref="ActiveOnly"/> instead. Two ideas, two fields.
    /// </para>
    /// </summary>
    public IReadOnlyCollection<NoticeStatus> Statuses { get; init; } =
        [NoticeStatus.Published, NoticeStatus.Superseded];

    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public string? CountryCode { get; init; }

    /// <summary>One notice category key.</summary>
    public string? CategoryKey { get; init; }

    /// <summary>
    /// Several category keys at once, for a caller browsing a set rather than one.
    /// <para>
    /// The same shape B8.1 added to the guide filter, and for the same reason: expressing "the four
    /// immigration-adjacent categories" as four queries would mean paging each separately and merging
    /// the pages, which no cursor can do correctly.
    /// </para>
    /// </summary>
    public IReadOnlyCollection<string> CategoryKeys { get; init; } = [];

    public IssuingAuthorityId? AuthorityId { get; init; }

    /// <summary>Which severities count. Empty means all of them.</summary>
    public IReadOnlyCollection<NoticeSeverity> Severities { get; init; } = [];

    /// <summary>The lowest severity that counts, for "show me anything serious".</summary>
    public NoticeSeverity? MinimumSeverity { get; init; }

    /// <summary>A normalized BCP-47 tag the notice was officially issued in.</summary>
    public string? Language { get; init; }

    /// <summary>Free text over title and summary. Naive by design — the search seam owns real search.</summary>
    public string? Text { get; init; }

    /// <summary>Only notices from an authority Trust has confirmed.</summary>
    public bool OfficialOnly { get; init; }

    /// <summary>Only notices whose verification held up.</summary>
    public bool VerifiedOnly { get; init; }

    /// <summary>
    /// Only notices that are guidance right now — published, in effect, not expired, not superseded.
    /// <para>
    /// The one filter almost every reader-facing screen sets, and the reason it is a flag rather than
    /// three date comparisons at each call site. It needs <see cref="AsOf"/>, which the repository
    /// supplies from the clock when the caller does not.
    /// </para>
    /// </summary>
    public bool ActiveOnly { get; init; }

    /// <summary>Only notices whose expiry has passed. The editorial cleanup queue.</summary>
    public bool ExpiredOnly { get; init; }

    /// <summary>The moment "active" and "expired" are evaluated against.</summary>
    public DateTimeOffset? AsOf { get; init; }

    /// <summary>Only notices published on or after this instant.</summary>
    public DateTimeOffset? PublishedFrom { get; init; }

    /// <summary>Only notices published before this instant.</summary>
    public DateTimeOffset? PublishedBefore { get; init; }

    /// <summary>Only notices taking effect on or after this instant.</summary>
    public DateTimeOffset? EffectiveFrom { get; init; }

    /// <summary>
    /// Restricts the answer to a known set, preserving every other filter.
    /// <para>
    /// For the two callers that need it: a dashboard band resolving what an editor pinned, and a
    /// search engine handing back identifiers that still have to be filtered for visibility. Empty
    /// means no restriction — an empty set and an absent one are the same request.
    /// </para>
    /// </summary>
    public IReadOnlyCollection<Guid> NoticeIds { get; init; } = [];

    // ── Audience, computed before the query ─────────────────────────────────────────────────

    /// <summary>
    /// Whether the caller may see drafts, submissions and archived notices.
    /// <para>
    /// A single flag rather than an identifier, because unlike a guide a notice has no author whose
    /// own drafts they alone can see. An intelligence desk is a team, and everything in it is visible
    /// to everybody on it — which is a simpler rule and the one the editorial workflow assumes.
    /// </para>
    /// </summary>
    public bool ViewerIsEditor { get; init; }

    /// <summary>Whether archived notices are included at all. Editors only, and off by default.</summary>
    public bool IncludeArchived { get; init; }

    // ── Ordering and paging ─────────────────────────────────────────────────────────────────

    public NoticeSortOrder Sort { get; init; } = NoticeSortOrder.Priority;

    /// <summary>The limit, clamped to what one page may return.</summary>
    public int Limit { get; init; } = IntelligenceLimits.DefaultPageSize;

    public int EffectiveLimit => Math.Clamp(Limit, 1, IntelligenceLimits.MaxPageSize);
}
