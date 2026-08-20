using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Series;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Series;

/// <summary>One repeating event, if the caller may see it.</summary>
public sealed class GetEventSeriesQueryHandler
    : IQueryHandler<GetEventSeriesQuery, EventSeriesResponse>
{
    private readonly IEventSeriesRepository _series;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly EventSeriesReadContext _read;

    public GetEventSeriesQueryHandler(
        IEventSeriesRepository series,
        CommunityLifeAuthorizationService authorization,
        EventSeriesReadContext read)
    {
        _series = series;
        _authorization = authorization;
        _read = read;
    }

    public async Task<Result<EventSeriesResponse>> Handle(
        GetEventSeriesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var series = query.SeriesId is { } id
            ? await _series.GetAsync(EventSeriesId.From(id), cancellationToken)
            : await _series.GetBySlugAsync(query.Slug ?? string.Empty, cancellationToken);

        if (series is null || series.IsDeleted)
        {
            return Result.Failure<EventSeriesResponse>(CommunityLifeErrors.EventSeriesNotFound);
        }

        var readable = await _authorization.RequireSeriesReadAsync(series, cancellationToken);

        return readable.IsFailure
            ? Result.Failure<EventSeriesResponse>(readable.Error)
            : Result.Success(await _read.ToResponseAsync(series, cancellationToken));
    }
}
