using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Analytics.Domain.Dimensions;

/// <summary>
/// What analytics knows about one piece of content.
/// <para>
/// <b>A dimension, not a copy of the content.</b> The distinction is the whole of §3 of the phase
/// brief and it is easy to lose: this row exists to answer "whose is it, what kind is it, what
/// should the tile say", and it is populated from the publication contract each module already
/// broadcasts. It is never read as the current state of anything. It carries no body, no price, no
/// availability, no moderation state and no visibility — a caller wanting any of those asks the
/// module that owns them.
/// </para>
/// <para>
/// <b>Why analytics needs it at all.</b> Two reasons, and the second is the load-bearing one. A
/// content-performance row has to render a title, and refusing to store one would mean either a
/// fan-out to six modules per page or a list of identifiers. And every owner-scoped authorization
/// decision in this module is "does the caller own this subject" — which needs an owner that does
/// not depend on the owning module being reachable, because an analytics read that fails when
/// Guides is degraded is an analytics read that fails.
/// </para>
/// <para>
/// <b><see cref="OwnerUserId"/> is as-of the last publication, and that is deliberate.</b> Content
/// that changes hands changes owner here on its next publication and not before. The alternative —
/// re-attributing history — would move figures a creator has already seen, and §20 of the brief
/// forbids exactly that.
/// </para>
/// </summary>
public sealed class AnalyticsContentDimension : AggregateRoot<AnalyticsContentDimensionId>
{
    private AnalyticsContentDimension(
        AnalyticsContentDimensionId id,
        AnalyticsContentType contentType,
        Guid contentId,
        Guid ownerUserId,
        DateTimeOffset firstSeenAt) : base(id)
    {
        ContentType = contentType;
        ContentId = contentId;
        OwnerUserId = ownerUserId;
        FirstSeenAt = firstSeenAt;
        LastUpdatedAt = firstSeenAt;
    }

    private AnalyticsContentDimension() { }

    public AnalyticsContentType ContentType { get; private set; }

    public Guid ContentId { get; private set; }

    /// <summary>
    /// The member the content is attributed to.
    /// <para>
    /// A <em>user</em> identifier rather than a creator, seller, employer or landlord identifier,
    /// because those are four different modules' notions of the same person and analytics attributes
    /// to the person. Every publication contract on this platform carries one.
    /// </para>
    /// </summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>What the tile says. Absent where the producing contract carries no title.</summary>
    public string? Title { get; private set; }

    /// <summary>The producing module's own status word, echoed. Never interpreted here.</summary>
    public string? Status { get; private set; }

    /// <summary>The catalogue key the content was filed under, as of publication.</summary>
    public string? CategoryKey { get; private set; }

    /// <summary>ISO 3166-1 alpha-2 as of publication, upper case.</summary>
    public string? CountryCode { get; private set; }

    /// <summary>The organization or community the content belongs to, where it belongs to one.</summary>
    public Guid? CommunityId { get; private set; }

    /// <summary>When it first became readable by somebody other than its owner.</summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>When analytics first heard of it. Not the same as publication — see the class summary.</summary>
    public DateTimeOffset FirstSeenAt { get; private set; }

    public DateTimeOffset LastUpdatedAt { get; private set; }

    /// <summary>
    /// EF concurrency token.
    /// <para>
    /// Two consumers can legitimately touch one dimension at once — a republication and an
    /// engagement whose content was previously unknown — and last-writer-wins on a dimension is
    /// fine, but a lost update that dropped an owner would strand every row that joins through it.
    /// </para>
    /// </summary>
    public int Revision { get; private set; }

    public static AnalyticsContentDimension Observe(
        AnalyticsContentDimensionId id,
        AnalyticsContentType contentType,
        Guid contentId,
        Guid ownerUserId,
        DateTimeOffset firstSeenAt,
        string? title = null,
        string? status = null,
        string? categoryKey = null,
        string? countryCode = null,
        Guid? communityId = null,
        DateTimeOffset? publishedAt = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        return new AnalyticsContentDimension(id, contentType, contentId, ownerUserId, firstSeenAt)
        {
            Title = Trim(title),
            Status = Trim(status),
            CategoryKey = Trim(categoryKey),
            CountryCode = Trim(countryCode)?.ToUpperInvariant(),
            CommunityId = communityId == Guid.Empty ? null : communityId,
            PublishedAt = publishedAt
        };
    }

    /// <summary>
    /// Folds a later publication in.
    /// <para>
    /// <b>Null means "leave it alone", which is not a convenience.</b> The engagement contracts
    /// carry an author and nothing else, so a share arriving for content analytics has never seen
    /// would otherwise blank the title a publication had already supplied. The same rule the creator
    /// profile edit follows, and for the same reason.
    /// </para>
    /// <para>
    /// <see cref="PublishedAt"/> only ever moves <em>earlier</em>. A republication is not a new
    /// publication, and a content-published metric that counted every edit would report a productive
    /// week for somebody who fixed a typo eight times.
    /// </para>
    /// </summary>
    public void Update(
        DateTimeOffset at,
        Guid? ownerUserId = null,
        string? title = null,
        string? status = null,
        string? categoryKey = null,
        string? countryCode = null,
        Guid? communityId = null,
        DateTimeOffset? publishedAt = null)
    {
        if (ownerUserId is not null && ownerUserId != Guid.Empty)
        {
            OwnerUserId = ownerUserId.Value;
        }

        Title = Trim(title) ?? Title;
        Status = Trim(status) ?? Status;
        CategoryKey = Trim(categoryKey) ?? CategoryKey;
        CountryCode = Trim(countryCode)?.ToUpperInvariant() ?? CountryCode;

        if (communityId is not null && communityId != Guid.Empty)
        {
            CommunityId = communityId;
        }

        if (publishedAt is not null && (PublishedAt is null || publishedAt < PublishedAt))
        {
            PublishedAt = publishedAt;
        }

        LastUpdatedAt = at;
        Revision++;
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
