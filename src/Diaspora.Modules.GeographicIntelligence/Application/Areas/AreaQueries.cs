using Diaspora.Application.Abstractions;
using Diaspora.Modules.GeographicIntelligence.Application.Services;
using Diaspora.Modules.GeographicIntelligence.Contracts.V1.Responses;
using Diaspora.Modules.GeographicIntelligence.Domain.Errors;
using Diaspora.Modules.GeographicIntelligence.Observability;
using Diaspora.Modules.GeographicIntelligence.Options;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.GeographicIntelligence.Application.Areas;

/// <summary>
/// The area around one point: counts, rent and safety within a circle (14 Oct 2026).
/// </summary>
[Authorize(Policy = GeographicIntelligencePolicies.IntelligenceRead)]
public sealed record GetAreaReportQuery : IQuery<AreaReportResponse>
{
    public required AreaPoint Point { get; init; }

    /// <summary>Kilometres. The configured default (2 km) when null; refused above the cap (10 km).</summary>
    public double? RadiusKm { get; init; }
}

public sealed class GetAreaReportQueryHandler : IQueryHandler<GetAreaReportQuery, AreaReportResponse>
{
    private readonly AreaReportComposer _composer;

    public GetAreaReportQueryHandler(AreaReportComposer composer) => _composer = composer;

    public async Task<Result<AreaReportResponse>> Handle(
        GetAreaReportQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        GeographicIntelligenceTelemetry.Requests.Add(
            1, new KeyValuePair<string, object?>("surface", "area"));

        var radius = _composer.ResolveRadius(request.RadiusKm);

        if (radius.IsFailure)
        {
            return Result.Failure<AreaReportResponse>(radius.Error);
        }

        return await _composer.ComposeAsync(request.Point, radius.Value, cancellationToken);
    }
}

/// <summary>
/// Several areas around points, composed identically with one radius, in the caller's order.
/// </summary>
[Authorize(Policy = GeographicIntelligencePolicies.IntelligenceRead)]
public sealed record CompareAreasQuery : IQuery<AreaComparisonResponse>
{
    public required IReadOnlyList<AreaPoint> Points { get; init; }

    public double? RadiusKm { get; init; }
}

/// <summary>
/// Composes each point and returns them side by side.
/// <para>
/// <b>The server compares nothing and ranks nothing</b>, for the reason the neighbourhood comparison
/// gives: each area arrives with the same figures and the same gaps stated, which makes a fair
/// comparison possible without one being asserted. Unlike a catalogue comparison, a point cannot
/// fail to resolve, so an invalid point fails the request rather than being dropped.
/// </para>
/// </summary>
public sealed class CompareAreasQueryHandler : IQueryHandler<CompareAreasQuery, AreaComparisonResponse>
{
    private readonly AreaReportComposer _composer;
    private readonly IOptions<GeographicIntelligenceOptions> _options;

    public CompareAreasQueryHandler(
        AreaReportComposer composer, IOptions<GeographicIntelligenceOptions> options)
    {
        _composer = composer;
        _options = options;
    }

    public async Task<Result<AreaComparisonResponse>> Handle(
        CompareAreasQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        GeographicIntelligenceTelemetry.Requests.Add(
            1, new KeyValuePair<string, object?>("surface", "area-compare"));

        if (request.Points.Count == 0)
        {
            return Result.Failure<AreaComparisonResponse>(GeographicIntelligenceErrors.ComparisonEmpty);
        }

        var maximum = _options.Value.MaximumComparisonSubjects;

        if (request.Points.Count > maximum)
        {
            return Result.Failure<AreaComparisonResponse>(
                GeographicIntelligenceErrors.TooManySubjects(request.Points.Count, maximum));
        }

        var radius = _composer.ResolveRadius(request.RadiusKm);

        if (radius.IsFailure)
        {
            return Result.Failure<AreaComparisonResponse>(radius.Error);
        }

        var areas = new List<AreaReportResponse>(request.Points.Count);

        // In turn: every read reaches the one scoped DbContext.
        foreach (var point in request.Points)
        {
            var area = await _composer.ComposeAsync(point, radius.Value, cancellationToken);

            if (area.IsFailure)
            {
                return Result.Failure<AreaComparisonResponse>(area.Error);
            }

            areas.Add(area.Value);
        }

        return Result.Success(new AreaComparisonResponse { RadiusKm = radius.Value, Areas = areas });
    }
}
