using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Domain.Dimensions;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Identifiers;
using Diaspora.Modules.Analytics.Domain.Repositories;

namespace Diaspora.Modules.Analytics.Application.Ingestion;

/// <summary>
/// Keeps the content dimension current, and repairs rows that arrived before it.
/// <para>
/// <b>The repair is the interesting half.</b> A view can legitimately arrive before the publication
/// event that names the content's author: they travel on different paths, the outbox makes no
/// ordering promise, and the client emits a view the moment somebody opens a screen. A row written
/// without an owner is invisible to every creator query, and would stay invisible forever — so when
/// the dimension does arrive, the rows waiting on it are attributed.
/// </para>
/// <para>
/// <b>Bounded, and honestly so.</b> The repair takes a page of unattributed rows rather than all of
/// them. An unbounded update inside a consumer is a lock held for as long as the backlog, and a
/// consumer that times out is one the platform retries — producing the same unbounded update again.
/// The page size is generous relative to how many views one unpublished item can collect, and the
/// backfill rebuilds anything the page missed.
/// </para>
/// </summary>
public sealed class AnalyticsDimensionRecorder
{
    /// <summary>How many stranded rows one publication repairs. See the class summary.</summary>
    private const int RepairPageSize = 500;

    private readonly IAnalyticsContentDimensionRepository _dimensions;
    private readonly IAnalyticsEventRepository _events;
    private readonly AnalyticsReportingCalendar _calendar;

    public AnalyticsDimensionRecorder(
        IAnalyticsContentDimensionRepository dimensions,
        IAnalyticsEventRepository events,
        AnalyticsReportingCalendar calendar)
    {
        _dimensions = dimensions;
        _events = events;
        _calendar = calendar;
    }

    /// <summary>
    /// Records what a publication says about a piece of content.
    /// <para>
    /// Idempotent by construction: a republication updates the row it finds rather than adding a
    /// second, and the fields it does not carry are left alone rather than blanked.
    /// </para>
    /// </summary>
    public async Task ObserveAsync(
        AnalyticsContentType contentType,
        Guid contentId,
        Guid ownerUserId,
        string? title = null,
        string? status = null,
        string? categoryKey = null,
        string? countryCode = null,
        Guid? communityId = null,
        DateTimeOffset? publishedAt = null,
        CancellationToken cancellationToken = default)
    {
        if (contentId == Guid.Empty)
        {
            return;
        }

        var now = _calendar.UtcNow;

        var existing = await _dimensions
            .FindAsync(contentType, contentId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            _dimensions.Add(AnalyticsContentDimension.Observe(
                AnalyticsContentDimensionId.New(),
                contentType,
                contentId,
                ownerUserId,
                now,
                title,
                status,
                categoryKey,
                countryCode,
                communityId,
                publishedAt));
        }
        else
        {
            existing.Update(
                now,
                ownerUserId,
                title,
                status,
                categoryKey,
                countryCode,
                communityId,
                publishedAt);
        }

        await RepairAsync(contentType, contentId, ownerUserId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Attributes rows that named this content before analytics knew whose it was.
    /// <para>
    /// The rows are loaded tracked and mutated in place; the caller's unit of work commits them
    /// alongside the dimension, so a failure leaves neither.
    /// </para>
    /// </summary>
    private async Task RepairAsync(
        AnalyticsContentType contentType,
        Guid contentId,
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        if (ownerUserId == Guid.Empty)
        {
            return;
        }

        var stranded = await _events
            .ListUnattributedAsync(contentType, contentId, RepairPageSize, cancellationToken)
            .ConfigureAwait(false);

        foreach (var row in stranded)
        {
            row.AttributeTo(ownerUserId, contentType);
        }
    }
}
