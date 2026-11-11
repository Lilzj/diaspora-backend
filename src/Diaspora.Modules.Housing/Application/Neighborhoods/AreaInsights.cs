using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.Modules.Housing.Observability;
using Diaspora.Modules.Housing.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Geography;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Housing.Application.Neighborhoods;

/// <summary>
/// Shares what a member knows about the area around a point (14 Oct 2026, Phase 2A of retiring the
/// place catalogue).
/// </summary>
[RequirePermission(HousingPermissions.InsightSubmit)]
[Auditable]
public sealed record SubmitAreaInsightCommand : ICommand<NeighborhoodInsightResponse>
{
    public required SubmitAreaInsightRequest Request { get; init; }
}

/// <summary>
/// Records an insight about a point, for a moderator to read.
/// <para>
/// <b>The same rules as an insight about a neighbourhood</b>: the per-day bound, the duplicate check
/// and moderation before anything renders, with the point in place of the area. The duplicate check
/// compares the same author's same words within <see cref="DuplicateRadiusKm"/> of the point,
/// because two points a few metres apart are the same place to the member who tapped them.
/// </para>
/// </summary>
public sealed class SubmitAreaInsightCommandHandler
    : ICommandHandler<SubmitAreaInsightCommand, NeighborhoodInsightResponse>
{
    /// <summary>How close a repeat of the same words must be to count as the same insight.</summary>
    public const double DuplicateRadiusKm = 0.5d;

    private readonly INeighborhoodInsightRepository _insights;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingAuthorizationService _authorization;
    private readonly IOptions<HousingOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public SubmitAreaInsightCommandHandler(
        INeighborhoodInsightRepository insights,
        IUnitOfWork unitOfWork,
        HousingAuthorizationService authorization,
        IOptions<HousingOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _insights = insights;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<NeighborhoodInsightResponse>> Handle(
        SubmitAreaInsightCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_options.Value.EnableCommunityInsights)
        {
            return Result.Failure<NeighborhoodInsightResponse>(HousingErrors.InsightNotFound);
        }

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<NeighborhoodInsightResponse>(caller.Error);
        }

        var request = command.Request;
        var now = _clock.UtcNow;

        if (!AreaInsightPoint.IsValid(request.Latitude, request.Longitude))
        {
            return Result.Failure<NeighborhoodInsightResponse>(HousingErrors.InsightPointInvalid);
        }

        var submittedToday = await _insights.CountByAuthorSinceAsync(
            caller.Value, now.AddDays(-1), cancellationToken);

        if (submittedToday >= _options.Value.MaxInsightsPerAuthorPerDay)
        {
            return Result.Failure<NeighborhoodInsightResponse>(HousingErrors.TooManyInsights);
        }

        var nearby = CircleArea.Around(request.Latitude, request.Longitude, DuplicateRadiusKm);

        if (await _insights.HasDuplicateNearAsync(
                caller.Value,
                NeighborhoodInsight.Fingerprints(request.Body),
                nearby.South,
                nearby.North,
                nearby.West,
                nearby.East,
                cancellationToken))
        {
            return Result.Failure<NeighborhoodInsightResponse>(HousingErrors.InsightDuplicate);
        }

        if (!Enum.TryParse<NeighborhoodInsightCategory>(
                request.Category, ignoreCase: true, out var category))
        {
            category = NeighborhoodInsightCategory.Other;
        }

        var insight = NeighborhoodInsight.SubmitAtPoint(
            NeighborhoodInsightId.From(_guids.NewSequentialGuid()),
            caller.Value,
            category,
            request.Body,
            request.Label,
            request.Latitude,
            request.Longitude,
            request.PlaceName,
            request.PlaceId,
            request.CountryCode,
            now);

        if (insight.IsFailure)
        {
            return Result.Failure<NeighborhoodInsightResponse>(insight.Error);
        }

        await _insights.AddAsync(insight.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        HousingTelemetry.InsightsSubmitted.Add(
            1, new KeyValuePair<string, object?>("category", category.ToString()));

        return Result.Success(NeighborhoodMapper.ToResponse(
            insight.Value, caller.Value, _authorization.IsStaff, hasEndorsed: false));
    }
}

/// <summary>
/// What members say about the area around a point: approved insights whose point is within the
/// circle (14 Oct 2026).
/// <para>
/// Insights written about a catalogue neighbourhood carry its centre, so they are found here too.
/// The audiences are the area list's: a visitor sees approved only, and an author asking for their
/// own sees their own whatever the status.
/// </para>
/// </summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record ListAreaInsightsQuery : IQuery<IReadOnlyList<NeighborhoodInsightResponse>>
{
    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    /// <summary>Kilometres. 2 when null; refused outside (0, 10].</summary>
    public double? RadiusKm { get; init; }

    public string? Category { get; init; }

    public bool MineOnly { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}

public sealed class ListAreaInsightsQueryHandler
    : IQueryHandler<ListAreaInsightsQuery, IReadOnlyList<NeighborhoodInsightResponse>>
{
    private readonly INeighborhoodInsightRepository _insights;
    private readonly HousingAuthorizationService _authorization;

    public ListAreaInsightsQueryHandler(
        INeighborhoodInsightRepository insights, HousingAuthorizationService authorization)
    {
        _insights = insights;
        _authorization = authorization;
    }

    public async Task<Result<IReadOnlyList<NeighborhoodInsightResponse>>> Handle(
        ListAreaInsightsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var callerId = _authorization.CallerId;

        if (request.MineOnly && callerId is null)
        {
            return Result.Failure<IReadOnlyList<NeighborhoodInsightResponse>>(HousingErrors.NotAuthenticated);
        }

        var radius = request.RadiusKm ?? AreaInsightPoint.DefaultRadiusKm;

        if (!AreaInsightPoint.IsValid(request.Latitude, request.Longitude)
            || double.IsNaN(radius) || radius <= 0d || radius > AreaInsightPoint.MaximumRadiusKm)
        {
            return Result.Failure<IReadOnlyList<NeighborhoodInsightResponse>>(HousingErrors.InsightPointInvalid);
        }

        var circle = CircleArea.Around(request.Latitude, request.Longitude, radius);

        var filter = new NeighborhoodInsightQueryFilter
        {
            MinLatitude = circle.South,
            MaxLatitude = circle.North,
            MinLongitude = circle.West,
            MaxLongitude = circle.East,
            AuthorId = request.MineOnly ? callerId : null,
            Categories = Enum.TryParse<NeighborhoodInsightCategory>(
                request.Category, ignoreCase: true, out var category)
                ? [category]
                : null,
            IncludeUnapproved = request.MineOnly,
            OrderByEndorsement = true,
            Limit = request.Limit ?? HousingLimits.DefaultPageSize
        };

        var page = await _insights.QueryAsync(filter, request.Cursor, filter.EffectiveLimit, cancellationToken);

        var rendered = new List<NeighborhoodInsightResponse>(page.Items.Count);

        // The rectangle's corners are not in the circle; trimmed here, over the page the index found.
        foreach (var insight in page.Items.Where(insight => circle.Contains(insight.Latitude, insight.Longitude)))
        {
            var endorsed = callerId is not null
                && await _insights.HasEndorsedAsync(insight.Id, callerId, cancellationToken);

            rendered.Add(NeighborhoodMapper.ToResponse(insight, callerId, _authorization.IsStaff, endorsed) with
            {
                DistanceKm = Math.Round(
                    circle.DistanceKm(insight.Latitude!.Value, insight.Longitude!.Value),
                    2,
                    MidpointRounding.AwayFromZero)
            });
        }

        return Result.Success<IReadOnlyList<NeighborhoodInsightResponse>>(rendered);
    }
}

/// <summary>The bounds a point-keyed insight read or write accepts.</summary>
public static class AreaInsightPoint
{
    /// <summary>The radius of "around here" when the client names none, matching the area page's.</summary>
    public const double DefaultRadiusKm = 2d;

    /// <summary>The widest circle a list describes, matching the area page's cap.</summary>
    public const double MaximumRadiusKm = 10d;

    public static bool IsValid(double latitude, double longitude) =>
        !double.IsNaN(latitude) && !double.IsInfinity(latitude) && latitude is >= -90d and <= 90d
        && !double.IsNaN(longitude) && !double.IsInfinity(longitude) && longitude is >= -180d and <= 180d;
}
