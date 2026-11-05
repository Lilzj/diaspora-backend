namespace Diaspora.Modules.Location.Domain.Towns;

public interface IWorldTownRepository
{
    /// <summary>
    /// Towns with a name beginning with <paramref name="start"/> (normalised), or, when
    /// <paramref name="anyWord"/>, with any word of a name beginning with it ("rose" in
    /// "beau bassin rose hill"); in the country when one is given, the most populous first.
    /// </summary>
    Task<IReadOnlyList<WorldTown>> CandidatesAsync(
        string start,
        string? countryCode,
        int limit,
        bool anyWord = false,
        CancellationToken cancellationToken = default);
}
