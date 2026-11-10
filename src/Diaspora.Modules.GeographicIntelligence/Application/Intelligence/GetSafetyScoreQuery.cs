using System.Globalization;
using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Integration;
using Diaspora.Modules.GeographicIntelligence.Application.Services;
using Diaspora.Modules.GeographicIntelligence.Contracts.V1.Responses;
using Diaspora.Modules.GeographicIntelligence.Domain.Errors;
using Diaspora.Modules.GeographicIntelligence.Domain.Safety;
using Diaspora.Modules.GeographicIntelligence.Domain.Scores;
using Diaspora.Modules.GeographicIntelligence.Observability;
using Diaspora.Modules.GeographicIntelligence.Options;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.GeographicIntelligence.Application.Intelligence;

/// <summary>The safety surface for a neighbourhood.</summary>
[Authorize(Policy = GeographicIntelligencePolicies.IntelligenceRead)]
public sealed record GetSafetyScoreQuery : IQuery<SafetyScoreResponse>
{
    public required string IdOrSlug { get; init; }

    public string? CountryCode { get; init; }
}

/// <summary>
/// Answers the safety question with sourced counts, and still no score.
/// <para>
/// <b>No score, by decision (16 Sep 2026).</b> A safety score materially affects where somebody
/// chooses to live, and this community includes people with the least margin for a bad move. The
/// user decided the platform publishes what named sources counted instead, each with its source,
/// period and method, shown separately and never blended into a number. The score fields stay null
/// with <c>NoDataSource</c>: no source measures safety, only incidents.
/// </para>
/// <para>
/// <b>Two sources.</b> Corroborated community safety reports — Police and Emergency alerts other
/// members confirmed — filed in the neighbourhood over the last <c>SafetyReportWindowDays</c>, for
/// every country. And, where data.police.uk covers the country, the street crime the police recorded
/// within one mile of the neighbourhood's centre over the most recent published months.
/// </para>
/// <para>
/// <b>An unreadable source is not a zero.</b> A police source that timed out, or that holds nothing for
/// the point, is published as unavailable with a reason. A member reading "0 crimes" about a place
/// the source does not cover would be reading the most dangerous figure this route could produce.
/// </para>
/// </summary>
public sealed class GetSafetyScoreQueryHandler
    : IQueryHandler<GetSafetyScoreQuery, SafetyScoreResponse>
{
    public const string CommunitySource = "Diaspora community reports";

    public const string PoliceSource = "data.police.uk";

    public const string PoliceLicence = "OGL-UK-3.0";

    public const string PoliceAttribution =
        "Contains public sector information licensed under the Open Government Licence v3.0.";

    /// <summary>Stands where a source's name would go when there is no source to name.</summary>
    public const string NoPoliceSource = "No published source";

    private readonly NeighborhoodResolver _resolver;
    private readonly ICommunitySafetyReports _reports;
    private readonly IPoliceCrimeData _police;
    private readonly ICountryRepository _countries;
    private readonly IOptions<GeographicIntelligenceOptions> _options;
    private readonly TimeProvider _clock;

    public GetSafetyScoreQueryHandler(
        NeighborhoodResolver resolver,
        ICommunitySafetyReports reports,
        IPoliceCrimeData police,
        ICountryRepository countries,
        IOptions<GeographicIntelligenceOptions> options,
        TimeProvider clock)
    {
        _resolver = resolver;
        _reports = reports;
        _police = police;
        _countries = countries;
        _options = options;
        _clock = clock;
    }

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

        var now = _clock.GetUtcNow();
        var name = area.Name.Value;

        var counts = new List<SafetyCountResponse>
        {
            await CommunityCountAsync(area.Path.Value, name, now, cancellationToken)
        };

        if (_police.Covers(area.CountryCode.Value))
        {
            counts.Add(await PoliceCountAsync(area.Center, name, cancellationToken));
        }
        else
        {
            var country = await _countries.GetByCodeAsync(area.CountryCode, cancellationToken);

            counts.Add(PoliceNotPublished(country?.Name.Value ?? area.CountryCode.Value));
        }

        return Result.Success(new SafetyScoreResponse
        {
            NeighborhoodId = area.Slug.Value,
            AreaId = area.Id.Value,
            Name = name,
            IsAvailable = false,
            Score = null,
            Band = null,
            Breakdown = [],
            Trend = null,
            UnavailableReason = DimensionUnavailableReason.NoDataSource.ToString(),
            Counts = counts,
            Limitations =
            [
                "Diaspora does not publish a safety score. It publishes what named sources counted, "
                + "separately, and none of those counts measures how safe a place is.",
                "Community reports are alerts members filed and other members corroborated. Many "
                + "incidents are never reported on Diaspora, and a quiet place on Diaspora may simply "
                + "have few members.",
                "Police-recorded crime covers England, Wales and Northern Ireland only, counts what was "
                + "reported to and recorded by the police, and places each crime at an approximate, "
                + "anonymised point. Everywhere else, including Scotland, has no official source "
                + "Diaspora may republish, and says so rather than showing nothing.",
                "Neither says anything about a particular street, a time of day, or what it is like to "
                + "live there, and neither should be the only basis for a decision about where to live."
            ]
        });
    }

    private async Task<SafetyCountResponse> CommunityCountAsync(
        string path, string name, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var window = TimeSpan.FromDays(_options.Value.SafetyReportWindowDays);
        var reports = await _reports.CountCorroboratedAsync(path, now, window, cancellationToken);

        return new SafetyCountResponse
        {
            Kind = "community-reports",
            Source = CommunitySource,
            IsAvailable = true,
            From = reports.From,
            To = reports.To,
            Area = string.Format(CultureInfo.InvariantCulture, "Alerts filed in {0}", name),
            Method = string.Format(
                CultureInfo.InvariantCulture,
                "Police and Emergency alerts members published in {0} over the last {1} days that other "
                + "members corroborated enough for Diaspora to mark them community-verified. Alerts their "
                + "authors withdrew, and anything moderators removed, are not counted.",
                name,
                _options.Value.SafetyReportWindowDays),
            Total = reports.Total,
            Categories =
            [
                .. reports.ByCategory
                    .OrderByDescending(entry => entry.Value)
                    .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                    .Select(entry => new SafetyCountCategoryResponse
                    {
                        Id = entry.Key,
                        Label = entry.Key,
                        Count = entry.Value
                    })
            ]
        };
    }

    private async Task<SafetyCountResponse> PoliceCountAsync(
        Location.Domain.ValueObjects.GeoCoordinate? centre, string name, CancellationToken cancellationToken)
    {
        var area = string.Format(CultureInfo.InvariantCulture, "Within one mile of {0}'s centre", name);

        const string Method =
            "Street-level crimes recorded by the police within one mile of the neighbourhood's centre, "
            + "counted over the most recent months data.police.uk has published. Each crime is placed "
            + "at an anonymised point near where it happened.";

        if (centre is null)
        {
            return PoliceUnavailable(area, Method, "NoCentre");
        }

        var answer = await _police.CountAsync(centre.Latitude, centre.Longitude, cancellationToken);

        return FromPoliceAnswer(answer, area, Method);
    }

    /// <summary>
    /// What the police source said, as a sourced count — or why it said nothing. Shared with the
    /// area around a point, which asks the same source about a point the member chose.
    /// </summary>
    internal static SafetyCountResponse FromPoliceAnswer(PoliceCrimeAnswer answer, string area, string method)
    {
        ArgumentNullException.ThrowIfNull(answer);

        if (answer.Counts is not { } counts)
        {
            return PoliceUnavailable(area, method, answer.UnavailableReason ?? PoliceCrimeUnavailable.SourceUnavailable);
        }

        return new SafetyCountResponse
        {
            Kind = "police-recorded-crime",
            Source = PoliceSource,
            IsAvailable = true,
            From = MonthStart(counts.FirstMonth),
            To = MonthStart(counts.LastMonth)?.AddMonths(1).AddTicks(-1),
            Area = area,
            Method = method,
            Licence = PoliceLicence,
            Attribution = PoliceAttribution,
            Total = counts.Total,
            Categories =
            [
                .. counts.ByCategory
                    .OrderByDescending(entry => entry.Value)
                    .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                    .Select(entry => new SafetyCountCategoryResponse
                    {
                        Id = entry.Key,
                        Label = counts.CategoryNames.TryGetValue(entry.Key, out var label) ? label : entry.Key,
                        Count = entry.Value
                    })
            ]
        };
    }

    /// <summary>
    /// What a country no source covers is told: that nobody publishes the figure.
    /// <para>
    /// <b>Decided on 17 Sep 2026.</b> Leaving the section out entirely left a member to guess between
    /// "Diaspora did not look", "the police recorded nothing" and "this place is safe". Saying so
    /// costs one line and closes all three readings.
    /// </para>
    /// <para>
    /// What was looked for, on 17 Sep 2026: Statistics Mauritius publishes national totals only, under
    /// "all rights reserved" with no reuse licence. Police Scotland publishes multi-member-ward
    /// management information as spreadsheets and states no licence, and data.police.uk holds no
    /// Scottish street data. Neither can be republished here, so neither is.
    /// </para>
    /// </summary>
    internal static SafetyCountResponse PoliceNotPublished(string country) =>
        new()
        {
            Kind = "police-recorded-crime",
            Source = NoPoliceSource,
            IsAvailable = false,
            UnavailableReason = PoliceCrimeUnavailable.NotPublished,
            Area = string.Format(CultureInfo.InvariantCulture, "Places in {0}", country),
            Method = string.Format(
                CultureInfo.InvariantCulture,
                "Diaspora publishes police-recorded crime where an official source publishes it openly "
                + "and allows it to be republished. No such source covers {0}, so there is no figure "
                + "here — which is not a low one.",
                country)
        };

    internal static SafetyCountResponse PoliceUnavailable(string area, string method, string reason) =>
        new()
        {
            Kind = "police-recorded-crime",
            Source = PoliceSource,
            IsAvailable = false,
            UnavailableReason = reason,
            Area = area,
            Method = method,
            Licence = PoliceLicence,
            Attribution = PoliceAttribution
        };

    private static DateTimeOffset? MonthStart(string month) =>
        DateTimeOffset.TryParseExact(
            month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var start)
            ? start
            : null;
}
