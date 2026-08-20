using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Extensibility.Search;

namespace Diaspora.Modules.CommunityLife.Infrastructure.Search;

/// <summary>
/// The shipped event search: the database, through the repository.
/// <para>
/// <b>The seam exists so a deployment can put something better behind it, not because this is
/// inadequate.</b> An indexed query over a few hundred thousand events with a cursor is fast and
/// exactly consistent — a member who publishes an event finds it immediately, which no external
/// index can promise. What it cannot do is relevance ranking on free text, which is what a
/// deployment that installs one is buying.
/// </para>
/// <para>
/// <b>Returns identifiers and no facet counts.</b> The handler hydrates the rows, so a future engine
/// swapping in cannot serve a cancelled event out of a stale index. Facet counts would need a second
/// aggregate query per browse; a deployment that wants them registers an engine that gets them for
/// free from its index.
/// </para>
/// </summary>
public sealed class EfCommunityEventSearchEngine : ICommunityEventSearchEngine
{
    private readonly ICommunityEventRepository _events;

    public EfCommunityEventSearchEngine(ICommunityEventRepository events) => _events = events;

    public string EngineName => "database";

    public async Task<EventSearchResult> SearchAsync(
        EventQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var page = await _events.SearchAsync(filter, cancellationToken);

        return new EventSearchResult
        {
            // The score is the position: the repository already applied the caller's sort, and
            // inventing a relevance number the database did not compute would be a lie a client
            // could sort by.
            Hits = [.. page.Items.Select(item => new EventSearchHit(item.Id.Value, 0d))],
            NextCursor = page.NextCursor,
            HasMore = page.HasMore,
            TotalCount = page.TotalCount
        };
    }
}
