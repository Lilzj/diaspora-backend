using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Extensibility.Search;

namespace Diaspora.Modules.Guides.Infrastructure.Search;

/// <summary>
/// The shipped official notice search engine, answering from the write model.
/// <para>
/// <b>Search independence is achieved by making this a degenerate case rather than by pretending
/// this is a search engine.</b> It delegates to the repository's text filter, so its
/// <see cref="IndexAsync"/> and <see cref="RemoveAsync"/> are honest no-ops — the write model
/// <em>is</em> this engine's index, and there is nothing to keep in step. B8 and B8.1 each ship the
/// same shape, and it is what makes an OpenSearch adapter a replacement of one class rather than a
/// rewrite of a query layer.
/// </para>
/// <para>
/// <b>It is always available</b>, because it has no dependency that can be down that the request
/// itself does not already have. An external engine answers <c>false</c> when its index is
/// unreachable, and the caller turns that into a service-unavailable error rather than an empty
/// result — which on official information is the difference between "search is down" and "the
/// government has said nothing about this".
/// </para>
/// <para>
/// <b>It never scores</b>, and reports zero on every hit. A LIKE match has no meaningful relevance,
/// and inventing one would give the API a number consumers would start ordering by — at which point
/// swapping in a real engine changes results nobody expected to change.
/// </para>
/// </summary>
public sealed class EfOfficialNoticeSearchEngine : IOfficialNoticeSearchEngine
{
    private readonly IOfficialNoticeRepository _notices;

    public EfOfficialNoticeSearchEngine(IOfficialNoticeRepository notices) => _notices = notices;

    public string Name => "ef";

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public async Task<NoticeSearchOutcome> SearchAsync(
        NoticeSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var page = await _notices.ListAsync(ToFilter(request), request.Cursor, cancellationToken);

        return new NoticeSearchOutcome
        {
            Hits =
            [
                .. page.Items.Select(notice => new NoticeSearchHit
                {
                    OfficialNoticeId = notice.Id.Value
                })
            ],
            NextCursor = page.NextCursor,
            HasMore = page.HasMore,

            // Deliberately absent. A cursor-paged relational query cannot produce a total without a
            // second full count, and a caller told "about 20 results" that turns out to mean 4,000
            // has been misled by a number nobody needed.
            TotalCount = null,
            Engine = Name
        };
    }

    /// <summary>An honest no-op: the write model is this engine's index.</summary>
    public Task IndexAsync(Guid officialNoticeId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc cref="IndexAsync" />
    public Task RemoveAsync(Guid officialNoticeId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <summary>
    /// Turns a search request into the same filter every listing uses.
    /// <para>
    /// This is what makes search and browse incapable of disagreeing about visibility. An engine that
    /// composed its own predicates would eventually answer a search with a draft notice, and it would
    /// do so on the one screen where nobody is looking for that failure.
    /// </para>
    /// </summary>
    private static NoticeQueryFilter ToFilter(NoticeSearchRequest request) =>
        new()
        {
            Text = request.Text,
            CountryCode = request.CountryCode,
            CategoryKeys = request.CategoryKeys,
            AuthorityId = Guid.TryParse(request.AuthorityId, out var authority)
                ? IssuingAuthorityId.From(authority)
                : null,
            Severities = ParseSeverities(request.Severities),
            Language = request.Language,
            OfficialOnly = request.OfficialOnly,
            ActiveOnly = request.ActiveOnly,
            ExpiredOnly = request.ExpiredOnly,
            PublishedFrom = ParseInstant(request.PublishedFrom),
            EffectiveFrom = ParseInstant(request.EffectiveFrom),
            Sort = ParseSort(request.Sort),
            Limit = request.Limit
        };

    /// <summary>
    /// Severity names to enum values, dropping anything unrecognised.
    /// <para>
    /// Dropping rather than failing, because these arrive as strings from a client and an unknown
    /// name is far more likely to be an old app version than an attack. Dropping an unknown name
    /// widens the answer, which is the safe direction for a filter on official information.
    /// </para>
    /// </summary>
    private static IReadOnlyCollection<NoticeSeverity> ParseSeverities(
        IReadOnlyList<string> names) =>
    [
        .. names
            .Select(name => Enum.TryParse<NoticeSeverity>(name, ignoreCase: true, out var parsed)
                ? parsed
                : (NoticeSeverity?)null)
            .OfType<NoticeSeverity>()
            .Distinct()
    ];

    private static DateTimeOffset? ParseInstant(string? value) =>
        DateTimeOffset.TryParse(
            value,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;

    /// <summary>
    /// A sort key to an ordering. Unknown and <c>relevance</c> both fall through to the briefing
    /// order, because this engine has no relevance to offer and the briefing order is the honest
    /// substitute.
    /// </summary>
    private static NoticeSortOrder ParseSort(string? sort) =>
        sort?.Trim().ToLowerInvariant() switch
        {
            "newest" => NoticeSortOrder.Newest,
            "oldest" => NoticeSortOrder.Oldest,
            "effective-soonest" => NoticeSortOrder.EffectiveSoonest,
            "expiring-soonest" => NoticeSortOrder.ExpiringSoonest,
            "most-viewed" => NoticeSortOrder.MostViewed,
            _ => NoticeSortOrder.Priority
        };
}
