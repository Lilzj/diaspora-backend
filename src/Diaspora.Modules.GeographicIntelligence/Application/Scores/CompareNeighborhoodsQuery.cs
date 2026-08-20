using Diaspora.Application.Abstractions;
using Diaspora.Modules.GeographicIntelligence.Application.Services;
using Diaspora.Modules.GeographicIntelligence.Contracts.V1.Responses;
using Diaspora.Modules.GeographicIntelligence.Domain.Errors;
using Diaspora.Modules.GeographicIntelligence.Observability;
using Diaspora.Modules.GeographicIntelligence.Options;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.GeographicIntelligence.Application.Scores;

/// <summary>Several neighbourhoods, composed the same way, for a side-by-side surface.</summary>
public sealed record CompareNeighborhoodsQuery : IQuery<IReadOnlyList<NeighborhoodScoreResponse>>
{
    public required IReadOnlyList<string> IdsOrSlugs { get; init; }

    public string? CountryCode { get; init; }
}

/// <summary>
/// Composes each subject and returns them together.
/// <para>
/// <b>The server compares nothing and ranks nothing.</b> The frontend register asks for
/// "server-side comparison, so the client is not blending scores it did not compute" — the part
/// that matters is the blending, and that is now the server's. Declaring a winner is a different
/// act: a composite judgement about where somebody should live, from dimensions this platform
/// itself says are incomplete. Each subject arrives with the same dimensions, the same methodology
/// and the same gaps stated, which is what makes a fair comparison possible without one being
/// asserted.
/// </para>
/// <para>
/// <b>Order is the caller's, and unresolvable subjects are dropped rather than failing the
/// request.</b> A comparison of three where one slug has been renamed should show two, not
/// nothing — but asking for none at all is a client defect and is refused.
/// </para>
/// </summary>
public sealed class CompareNeighborhoodsQueryHandler
    : IQueryHandler<CompareNeighborhoodsQuery, IReadOnlyList<NeighborhoodScoreResponse>>
{
    private readonly NeighborhoodResolver _resolver;
    private readonly NeighborhoodScoreComposer _composer;
    private readonly IOptions<GeographicIntelligenceOptions> _options;

    public CompareNeighborhoodsQueryHandler(
        NeighborhoodResolver resolver,
        NeighborhoodScoreComposer composer,
        IOptions<GeographicIntelligenceOptions> options)
    {
        _resolver = resolver;
        _composer = composer;
        _options = options;
    }

    public async Task<Result<IReadOnlyList<NeighborhoodScoreResponse>>> Handle(
        CompareNeighborhoodsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        GeographicIntelligenceTelemetry.Requests.Add(
            1, new KeyValuePair<string, object?>("surface", "compare"));

        var subjects = request.IdsOrSlugs
            .Select(value => value?.Trim())
            .Where(value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (subjects.Count == 0)
        {
            return Result.Failure<IReadOnlyList<NeighborhoodScoreResponse>>(
                GeographicIntelligenceErrors.ComparisonEmpty);
        }

        var maximum = _options.Value.MaximumComparisonSubjects;

        if (subjects.Count > maximum)
        {
            GeographicIntelligenceTelemetry.BoundsRejected.Add(
                1, new KeyValuePair<string, object?>("surface", "compare"));

            return Result.Failure<IReadOnlyList<NeighborhoodScoreResponse>>(
                GeographicIntelligenceErrors.TooManySubjects(subjects.Count, maximum));
        }

        var composed = new List<NeighborhoodScoreResponse>(subjects.Count);

        // In turn, not in parallel. Every read below reaches the one scoped DbContext, and EF Core
        // refuses a second operation while the first is running — the defect Wave 4 found in two of
        // Location's own aggregators, where the catch turned each overlap into a silently omitted
        // vertical. SQLite's provider hides it because its async is synchronous; production is not.
        foreach (var subject in subjects)
        {
            var area = await _resolver.ResolveAsync(subject, request.CountryCode, cancellationToken);

            if (area is null)
            {
                continue;
            }

            composed.Add(await _composer.ComposeAsync(area, cancellationToken));
        }

        return Result.Success<IReadOnlyList<NeighborhoodScoreResponse>>(composed);
    }
}
