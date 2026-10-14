using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Guides.Domain.Guides;

/// <summary>
/// Everything a discovery query can ask the guide table for.
/// <para>
/// A filter record rather than an <c>IQueryable</c>, for the reason B7's equivalent documents:
/// a contributed ranking strategy composes a filter and hands it back; it never composes an
/// expression tree, never touches a <c>DbContext</c>, and cannot write a predicate EF fails to
/// translate at run time inside somebody else's stack trace. Every field here is one the
/// repository knows how to translate — a promise the repository can actually keep.
/// </para>
/// <para>
/// Visibility is computed <em>before</em> the query, never in it: <see cref="ViewerId"/> and
/// <see cref="ViewerIsStaff"/> tell the repository whose drafts, unlisted and hidden guides may
/// appear, and the repository turns that into predicates. No caller composes its own.
/// </para>
/// </summary>
public sealed record GuideQueryFilter
{
    /// <summary>Which statuses count. Published only, unless a caller says otherwise.</summary>
    public IReadOnlyCollection<GuideStatus> Statuses { get; init; } = [GuideStatus.Published];

    /// <summary>Free text over title, summary and tags. Naive by design — the search seam owns real search.</summary>
    public string? Text { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public string? CountryCode { get; init; }

    /// <summary>A town, matched case-insensitively against the town the author typed.</summary>
    public string? CityName { get; init; }

    /// <summary>
    /// A rectangle over the guide's own coordinates — the rectangle around a circle search
    /// (14 Oct 2026). A guide with no coordinates is outside it unless it names
    /// <see cref="UnpinnedCityName"/>.
    /// </summary>
    public double? MinLatitude { get; init; }

    public double? MaxLatitude { get; init; }

    public double? MinLongitude { get; init; }

    public double? MaxLongitude { get; init; }

    /// <summary>
    /// With a rectangle: a town whose unpinned guides are kept, compared case-insensitively.
    /// </summary>
    public string? UnpinnedCityName { get; init; }

    /// <summary>Whether all four edges of the rectangle are set.</summary>
    public bool HasBoundingBox =>
        MinLatitude is not null && MaxLatitude is not null
        && MinLongitude is not null && MaxLongitude is not null;

    /// <summary>Whether the rectangle wraps across ±180°.</summary>
    public bool BoundingBoxCrossesAntimeridian => HasBoundingBox && MinLongitude > MaxLongitude;

    public string? CategoryKey { get; init; }

    /// <summary>
    /// Several category keys at once, for a caller that browses a set rather than one.
    /// <para>
    /// <b>Added by B8.1, and it is what lets the Knowledge Directory curate without duplicating.</b>
    /// A directory navigation row draws from one or more filing categories — "Getting Started" from
    /// <c>getting-started</c> and <c>orientation</c> — and expressing that as several queries would
    /// mean paging each separately and merging the pages, which no cursor can do correctly.
    /// Combined with <see cref="CategoryKey"/> by AND, though no caller sets both.
    /// </para>
    /// </summary>
    public IReadOnlyCollection<string> CategoryKeys { get; init; } = [];

    /// <summary>
    /// Restricts the answer to a known set of guides, preserving every other filter.
    /// <para>
    /// Added by B8.1 for two callers: a curated shelf resolving the guides an editor placed, and a
    /// search engine handing back identifiers that still have to be filtered for visibility. Empty
    /// means no restriction — an empty <em>set</em> and an absent one are the same request, because
    /// the alternative is a nullable collection every call site has to guard.
    /// </para>
    /// </summary>
    public IReadOnlyCollection<Guid> GuideIds { get; init; } = [];

    /// <summary>A normalized tag slug.</summary>
    public string? Tag { get; init; }

    /// <summary>A lower-case ISO 639-1 code.</summary>
    public string? Language { get; init; }

    public UserId? AuthorId { get; init; }

    /// <summary>The client's "verified guides only" toggle.</summary>
    public bool VerifiedOnly { get; init; }

    /// <summary>
    /// Verified <em>and</em> carrying a badge from Trust.
    /// <para>
    /// <b>Added by B8.1, and narrower than <see cref="VerifiedOnly"/> on purpose.</b> A verified
    /// guide has been checked; an official one came from the body that makes the rule, and Trust
    /// records that as the badge on the mark. The distinction matters to somebody deciding whether
    /// to act on what they are reading, which is most of why they are reading it.
    /// </para>
    /// </summary>
    public bool OfficialOnly { get; init; }

    /// <summary>The featured carousel.</summary>
    public bool FeaturedOnly { get; init; }

    /// <summary>
    /// Restricts to one reading difficulty. Added by B8.1 for its beginner and advanced shelves.
    /// </summary>
    public GuideDifficulty? Difficulty { get; init; }

    /// <summary>
    /// The longest read the caller will accept, in minutes.
    /// <para>
    /// Added by B8.1 because it is a real intent rather than a nicety: somebody standing in a queue
    /// at an immigration office has five minutes, and a forty-minute guide is the wrong answer
    /// however relevant it is.
    /// </para>
    /// </summary>
    public int? MaxReadingTimeMinutes { get; init; }

    // ── Audience, computed before the query ─────────────────────────────────────────────────

    /// <summary>The viewer, for including their own drafts on the "mine" screen.</summary>
    public UserId? ViewerId { get; init; }

    /// <summary>Whether the viewer sees hidden and flagged guides. Staff only.</summary>
    public bool ViewerIsStaff { get; init; }

    // ── Ordering and paging ─────────────────────────────────────────────────────────────────

    public GuideSortOrder Sort { get; init; } = GuideSortOrder.Newest;

    /// <summary>The limit, clamped to what one page may return.</summary>
    public int Limit { get; init; } = GuideLimits.DefaultPageSize;

    public int EffectiveLimit => Math.Clamp(Limit, 1, GuideLimits.MaxPageSize);
}
