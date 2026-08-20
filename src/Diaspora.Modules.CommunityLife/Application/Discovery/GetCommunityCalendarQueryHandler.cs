using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Extensibility.Calendar;
using Diaspora.Modules.CommunityLife.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.CommunityLife.Application.Discovery;

/// <summary>
/// Everything happening in a window, from every registered source.
/// <para>
/// <b>The module's own events are one source among several.</b> A deployment can add public
/// holidays, a consulate's opening times or a national day calendar, and they appear beside the
/// events without this module knowing what any of them are. Which sources answered is returned, so a
/// client can say where an entry came from.
/// </para>
/// <para>
/// <b>A source that fails is skipped, not fatal.</b> A calendar that went blank because one
/// contributed feed timed out would be worse than one missing a national holiday — and the source
/// most likely to fail is the one calling somebody else's API.
/// </para>
/// <para>
/// <b>The window is clamped.</b> An unbounded range would have every source scan its whole history,
/// and a client asking for ten years of a public-holiday feed is a client that has lost a date
/// picker rather than one that wants ten years.
/// </para>
/// </summary>
public sealed class GetCommunityCalendarQueryHandler
    : IQueryHandler<GetCommunityCalendarQuery, CommunityCalendarResponse>
{
    private readonly IReadOnlyList<ICommunityCalendarSource> _sources;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly IOptions<CommunityLifeOptions> _options;
    private readonly ILogger<GetCommunityCalendarQueryHandler> _logger;
    private readonly IClock _clock;

    public GetCommunityCalendarQueryHandler(
        IEnumerable<ICommunityCalendarSource> sources,
        CommunityLifeAuthorizationService authorization,
        IOptions<CommunityLifeOptions> options,
        ILogger<GetCommunityCalendarQueryHandler> logger,
        IClock clock)
    {
        _sources = [.. sources];
        _authorization = authorization;
        _options = options;
        _logger = logger;
        _clock = clock;
    }

    public async Task<Result<CommunityCalendarResponse>> Handle(
        GetCommunityCalendarQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var now = _clock.UtcNow;
        var from = query.From ?? now;
        var maximum = TimeSpan.FromDays(_options.Value.MaxCalendarWindowDays);
        var to = query.To ?? from.Add(maximum);

        if (to <= from)
        {
            to = from.Add(maximum);
        }

        if (to - from > maximum)
        {
            to = from.Add(maximum);
        }

        var window = new CalendarWindow
        {
            From = from,
            To = to,
            CountryCode = query.CountryCode,
            NeighborhoodAreaId = query.NeighborhoodAreaId,
            ViewerUserId = _authorization.CallerId?.Value
        };

        var entries = new List<CalendarEntryResponse>();
        var answered = new List<string>();

        foreach (var source in _sources.Where(source => source.AppliesTo(window)))
        {
            var contributed = await SafeEntriesAsync(source, window, cancellationToken);

            if (contributed.Count == 0)
            {
                continue;
            }

            answered.Add(source.SourceKey);
            entries.AddRange(contributed.Select(entry => ToResponse(entry, source.SourceKey)));
        }

        return Result.Success(new CommunityCalendarResponse
        {
            From = from,
            To = to,
            CountryCode = query.CountryCode,
            NeighborhoodAreaId = query.NeighborhoodAreaId,
            Entries = [.. entries.OrderBy(entry => entry.StartsAt).ThenBy(entry => entry.Title)],
            Sources = answered
        });
    }

    private async Task<IReadOnlyList<CalendarEntry>> SafeEntriesAsync(
        ICommunityCalendarSource source,
        CalendarWindow window,
        CancellationToken cancellationToken)
    {
        try
        {
            return await source.GetEntriesAsync(window, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A contributed source must not take down the calendar.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Calendar source {Source} failed. Its entries are missing from this window.",
                source.SourceKey);

            return [];
        }
#pragma warning restore CA1031
    }

    private static CalendarEntryResponse ToResponse(CalendarEntry entry, string sourceKey) =>
        new()
        {
            Id = entry.Id,
            Title = entry.Title,
            StartsAt = entry.StartsAt,
            EndsAt = entry.EndsAt,
            IsAllDay = entry.IsAllDay,
            EntryType = entry.EntryType,
            Description = entry.Description,
            CountryCode = entry.CountryCode,
            EventId = entry.EventId,
            SourceKey = sourceKey
        };
}
