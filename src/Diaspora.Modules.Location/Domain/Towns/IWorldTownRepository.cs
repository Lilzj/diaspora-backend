namespace Diaspora.Modules.Location.Domain.Towns;

public interface IWorldTownRepository
{
    /// <summary>
    /// Towns with a name beginning with <paramref name="start"/> (normalised), in the country
    /// when one is given, the most populous first.
    /// </summary>
    Task<IReadOnlyList<WorldTown>> CandidatesAsync(
        string start,
        string? countryCode,
        int limit,
        CancellationToken cancellationToken = default);
}
