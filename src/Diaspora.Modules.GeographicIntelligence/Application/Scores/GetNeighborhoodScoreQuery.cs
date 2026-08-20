using Diaspora.Application.Abstractions;
using Diaspora.Modules.GeographicIntelligence.Application.Services;
using Diaspora.Modules.GeographicIntelligence.Contracts.V1.Responses;
using Diaspora.Modules.GeographicIntelligence.Domain.Errors;
using Diaspora.Modules.GeographicIntelligence.Observability;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.GeographicIntelligence.Application.Scores;

/// <summary>The score breakdown for one neighbourhood.</summary>
public sealed record GetNeighborhoodScoreQuery : IQuery<NeighborhoodScoreResponse>
{
    /// <summary>B4's area identifier or its slug. Identifier wins where both would parse.</summary>
    public required string IdOrSlug { get; init; }

    /// <summary>Disambiguates a slug. There is a Springfield in twelve states.</summary>
    public string? CountryCode { get; init; }
}

public sealed class GetNeighborhoodScoreQueryHandler
    : IQueryHandler<GetNeighborhoodScoreQuery, NeighborhoodScoreResponse>
{
    private readonly NeighborhoodScoreComposer _composer;

    public GetNeighborhoodScoreQueryHandler(NeighborhoodScoreComposer composer) =>
        _composer = composer;

    public async Task<Result<NeighborhoodScoreResponse>> Handle(
        GetNeighborhoodScoreQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        GeographicIntelligenceTelemetry.Requests.Add(
            1, new KeyValuePair<string, object?>("surface", "scores"));

        var composed = await _composer.ComposeAsync(
            request.IdOrSlug, request.CountryCode, cancellationToken);

        if (composed is null)
        {
            GeographicIntelligenceTelemetry.Errors.Add(
                1,
                new KeyValuePair<string, object?>(
                    "code", GeographicIntelligenceErrorCodes.NeighborhoodNotFound));

            return Result.Failure<NeighborhoodScoreResponse>(
                GeographicIntelligenceErrors.NeighborhoodNotFound);
        }

        return Result.Success(composed);
    }
}
