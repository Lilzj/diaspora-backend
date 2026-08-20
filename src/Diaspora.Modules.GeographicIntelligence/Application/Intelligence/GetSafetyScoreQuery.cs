using Diaspora.Application.Abstractions;
using Diaspora.Modules.GeographicIntelligence.Application.Services;
using Diaspora.Modules.GeographicIntelligence.Contracts.V1.Responses;
using Diaspora.Modules.GeographicIntelligence.Domain.Errors;
using Diaspora.Modules.GeographicIntelligence.Domain.Scores;
using Diaspora.Modules.GeographicIntelligence.Observability;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.GeographicIntelligence.Application.Intelligence;

/// <summary>The safety surface for a neighbourhood.</summary>
public sealed record GetSafetyScoreQuery : IQuery<SafetyScoreResponse>
{
    public required string IdOrSlug { get; init; }

    public string? CountryCode { get; init; }
}

/// <summary>
/// Answers the safety question with the only true answer available: there is no safety score.
/// <para>
/// <b>The register rated this its highest-consequence item on a premise that is inverted.</b> Its
/// words were that <c>stats.safetyScore</c> "is real and is rendered as a real value", with only
/// the provenance missing. The column exists on B4's area, <b>nothing has ever written to it</b>,
/// B11's nine assessed bands deliberately exclude safety — a community-assessed safety rating being
/// a far more dangerous artefact than a rating of parking — and Housing's published summary
/// hard-codes <c>SafetyBand = null</c>. No data source is contracted anywhere.
/// </para>
/// <para>
/// <b>Serving the route anyway is the point.</b> The alternative is what exists today: no route,
/// which the client cannot distinguish from an outage, so it simulates the provenance and marks it.
/// This returns <c>isAvailable: false</c>, a branchable reason, and the platform's own limitations
/// text — so the safety screen can stop writing that text itself and start quoting the server, which
/// is what §26 actually asks for.
/// </para>
/// <para>
/// A safety score materially affects where somebody chooses to live, and this community includes
/// people with the least margin for a bad move. When a source is contracted, the fields on this
/// response start being populated and no contract changes.
/// </para>
/// </summary>
public sealed class GetSafetyScoreQueryHandler
    : IQueryHandler<GetSafetyScoreQuery, SafetyScoreResponse>
{
    private readonly NeighborhoodResolver _resolver;

    public GetSafetyScoreQueryHandler(NeighborhoodResolver resolver) => _resolver = resolver;

    public async Task<Result<SafetyScoreResponse>> Handle(
        GetSafetyScoreQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        GeographicIntelligenceTelemetry.Requests.Add(
            1, new KeyValuePair<string, object?>("surface", "safety"));

        var area = await _resolver.ResolveAsync(
            request.IdOrSlug, request.CountryCode, cancellationToken);

        if (area is null)
        {
            return Result.Failure<SafetyScoreResponse>(
                GeographicIntelligenceErrors.NeighborhoodNotFound);
        }

        GeographicIntelligenceTelemetry.DimensionsUnavailable.Add(
            1,
            new KeyValuePair<string, object?>("dimension", ScoreDimensionId.Safety),
            new KeyValuePair<string, object?>(
                "reason", DimensionUnavailableReason.NoDataSource.ToString()));

        return Result.Success(new SafetyScoreResponse
        {
            NeighborhoodId = area.Slug.Value,
            AreaId = area.Id.Value,
            Name = area.Name.Value,
            IsAvailable = false,
            Score = null,
            Band = null,
            Breakdown = [],
            Trend = null,
            UnavailableReason = DimensionUnavailableReason.NoDataSource.ToString(),
            Limitations =
            [
                "Diaspora does not publish a safety score. No crime, incident or safety data "
                + "source is contracted for any country Diaspora operates in.",
                "Neighbourhood profiles record how walkable, affordable, family-friendly and "
                + "accessible a place is. They deliberately do not record how safe it is.",
                "Any safety figure shown elsewhere in this application is an estimate produced by "
                + "the application itself and is not a Diaspora measurement.",
                "Members' own accounts of a neighbourhood are published as moderated insights and "
                + "should be read as individual experiences, not as a rating."
            ]
        });
    }
}
