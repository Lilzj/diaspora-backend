using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.CommunityLife.Domain.Series;

/// <summary>How the module reads and writes recurring series.</summary>
public interface IEventSeriesRepository
{
    Task<EventSeries?> GetAsync(EventSeriesId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// A series including a soft-deleted one.
    /// <para>
    /// The global query filter hides the row every restore has to load — the blind spot recorded
    /// platform-wide — so the counterpart exists here for the same reason it does on the event and
    /// organization repositories.
    /// </para>
    /// </summary>
    Task<EventSeries?> GetIncludingDeletedAsync(
        EventSeriesId id, CancellationToken cancellationToken = default);

    Task<EventSeries?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<CursorPagedResult<EventSeries>> SearchAsync(
        EventSeriesQueryFilter filter, CancellationToken cancellationToken = default);

    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>How many live series the organizer holds. The abuse bound a series most needs.</summary>
    Task<int> CountLiveByOrganizerAsync(
        UserId organizerUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Series whose horizon needs extending. The expansion sweep's work list.
    /// <para>
    /// Ordered by how far behind they are, so a deployment that has been down for a week catches up
    /// on the most overdue first rather than in identifier order.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<EventSeries>> FindNeedingExpansionAsync(
        DateOnly horizon, int batchSize, CancellationToken cancellationToken = default);

    Task AddAsync(EventSeries series, CancellationToken cancellationToken = default);

    void Update(EventSeries series);
}
