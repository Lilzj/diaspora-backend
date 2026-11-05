using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Towns;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Towns;

/// <summary>
/// Which town a typed place name means (owner, 5 Nov 2026): "Grand Baie" in "3 bedroom flat in
/// Grand Baie", "Grand Bay" too. The member's own country is asked first, because "Grand Bay" is
/// also a town in the United States; when nothing there is close enough, the whole world is.
/// <para>
/// At home a town is also found by the words inside its name: GeoNames files Rose Hill as "Beau
/// Bassin-Rose Hill", and without this "Rose Hill" from Mauritius found the one in the United
/// States, which the app then said was outside the member's country. Abroad only whole names count,
/// so a word inside a foreign town's name never stands for a town the member did not mean.
/// </para>
/// </summary>
[Authorize(Permission = LocationPermissions.Search)]
public sealed record MatchTownQuery : IQuery<TownMatchResponse>
{
    public required string Text { get; init; }

    /// <summary>The member's country, asked first. Optional.</summary>
    public string? CountryCode { get; init; }
}

public sealed class MatchTownQueryHandler : IQueryHandler<MatchTownQuery, TownMatchResponse>
{
    /// <summary>Enough to hold every town with a word that starts the same way in a country.</summary>
    private const int CountryCandidates = 1000;

    /// <summary>A part of a name never counts as the name itself, and never outranks it.</summary>
    private const double PartWeight = 0.99;

    private const int WorldCandidates = 2000;

    private readonly IWorldTownRepository _towns;

    public MatchTownQueryHandler(IWorldTownRepository towns) => _towns = towns;

    public async Task<Result<TownMatchResponse>> Handle(MatchTownQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var typed = TownNames.Normalize(query.Text);

        if (typed.Length < 2)
        {
            return Result.Success(TownMatchResponse.NotFound);
        }

        // Candidates share the first letters; a slip after them is what Closeness forgives.
        var start = typed[..Math.Min(3, typed.Length)];

        if (!string.IsNullOrWhiteSpace(query.CountryCode))
        {
            var atHome = await _towns.CandidatesAsync(
                start, query.CountryCode, CountryCandidates, anyWord: true, cancellationToken);

            if (Best(typed, atHome, wordsInsideNames: true) is { } home)
            {
                return Result.Success(home);
            }
        }

        var world = await _towns.CandidatesAsync(
            start, countryCode: null, WorldCandidates, anyWord: false, cancellationToken);

        return Result.Success(Best(typed, world, wordsInsideNames: false) ?? TownMatchResponse.NotFound);
    }

    private static TownMatchResponse? Best(string typed, IReadOnlyList<WorldTown> candidates, bool wordsInsideNames)
    {
        var best = candidates
            .Select(town => (Town: town, Score: town.Names.Max(name => Score(typed, name, wordsInsideNames))))
            .Where(scored => scored.Score >= TownNames.CloseEnough)
            .OrderByDescending(scored => scored.Score)
            .ThenByDescending(scored => scored.Town.Population)
            .FirstOrDefault();

        return best.Town is null
            ? null
            : new TownMatchResponse
            {
                Found = true,
                Name = best.Town.Name,
                CountryCode = best.Town.CountryCode,
                Latitude = best.Town.Latitude,
                Longitude = best.Town.Longitude,
                Population = best.Town.Population,
                Exact = best.Score >= 1
            };
    }

    private static double Score(string typed, string name, bool wordsInsideNames) =>
        wordsInsideNames
            ? Math.Max(TownNames.Closeness(typed, name), TownNames.PartCloseness(typed, name) * PartWeight)
            : TownNames.Closeness(typed, name);
}
