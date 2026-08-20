using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Domain.Series;
using Diaspora.Modules.CommunityLife.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Series;

/// <summary>
/// Announces a repeating event, and the dates already written for it.
/// <para>
/// <b>Publishing the series is not enough on its own.</b> A draft series writes its dates as drafts,
/// so a publication that moved only the series would leave a live pattern whose every occurrence was
/// invisible — the series page would say "next Tuesday" and next Tuesday would 404. Each occurrence
/// is published individually and raises its own event, because each is separately discoverable and
/// separately indexed.
/// </para>
/// <para>
/// Dates already past are left as drafts rather than published. Announcing something that finished
/// last week would put a stale date at the top of a discovery feed and reopen responses on it.
/// </para>
/// </summary>
public sealed class PublishEventSeriesCommandHandler
    : ICommandHandler<PublishEventSeriesCommand, EventSeriesResponse>
{
    private readonly IEventSeriesRepository _series;
    private readonly ICommunityEventRepository _events;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly EventSeriesExpansionService _expansion;
    private readonly EventSeriesReadContext _read;
    private readonly IClock _clock;

    public PublishEventSeriesCommandHandler(
        IEventSeriesRepository series,
        ICommunityEventRepository events,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        EventSeriesExpansionService expansion,
        EventSeriesReadContext read,
        IClock clock)
    {
        _series = series;
        _events = events;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _expansion = expansion;
        _read = read;
        _clock = clock;
    }

    public async Task<Result<EventSeriesResponse>> Handle(
        PublishEventSeriesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var series = await _series.GetAsync(
            EventSeriesId.From(command.SeriesId), cancellationToken);

        if (series is null)
        {
            return Result.Failure<EventSeriesResponse>(CommunityLifeErrors.EventSeriesNotFound);
        }

        var caller = await _authorization.RequireSeriesWriteAsync(series, cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<EventSeriesResponse>(caller.Error);
        }

        var now = _clock.UtcNow;
        var published = series.Publish(now, caller.Value);

        if (published.IsFailure)
        {
            return Result.Failure<EventSeriesResponse>(published.Error);
        }

        _series.Update(series);

        var occurrences = await _events.FindOccurrencesToRewriteAsync(
            series.Id,
            fromSequence: 1,
            notBefore: now,

            // Individually edited dates are published too. An organizer who moved one week's meeting
            // before announcing the pattern still meant it to be announced; skipping it would leave
            // exactly one invisible date in an otherwise live series.
            includeExceptions: true,
            CommunityLifeLimits.MaxSeriesOccurrences,
            cancellationToken);

        foreach (var occurrence in occurrences)
        {
            occurrence.SetVisibility(series.Visibility, baseRevision: null);

            if (occurrence.Publish(now, caller.Value).IsSuccess)
            {
                _events.Update(occurrence);
            }
        }

        // A series published before its horizon was reached — created as a draft weeks ago — has
        // fewer dates than it should. Topping up here means publishing produces a full calendar
        // rather than one that fills in whenever the sweep next runs.
        await _expansion.ExpandAsync(series, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        CommunityLifeTelemetry.SeriesPublished.Add(
            1,
            new KeyValuePair<string, object?>(
                "frequency", series.Recurrence.Frequency.ToString()));

        return Result.Success(await _read.ToResponseAsync(series, cancellationToken));
    }
}
