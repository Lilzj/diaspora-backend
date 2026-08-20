using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Providers.Extensibility.Ranking;

/// <summary>
/// What a ranking strategy knows about the request it is ordering results for.
/// <para>
/// <see cref="DistancesKm"/> is pre-computed and handed in rather than left for the strategy to
/// work out, and that is not a convenience — it is the boundary. Distances are Location's
/// answer, computed once by <c>ILocationDirectory</c> for the page; a strategy that did its own
/// haversine would be a second, subtly different distance in the same response, and the two
/// would disagree at the fourth decimal place in a way nobody could reproduce.
/// </para>
/// </summary>
public sealed record ProviderRankingContext
{
    /// <summary>The filter the candidates came from. Read-only context, not a re-query.</summary>
    public required ProviderQueryFilter Filter { get; init; }

    /// <summary>Now, so a strategy can weigh recency without reading a clock of its own.</summary>
    public required DateTimeOffset At { get; init; }

    /// <summary>The caller, when there is one.</summary>
    public UserId? ViewerId { get; init; }

    /// <summary>
    /// Distance in kilometres per provider, from Location. Empty when the scope was not spatial.
    /// </summary>
    public IReadOnlyDictionary<Guid, double> DistancesKm { get; init; } =
        new Dictionary<Guid, double>();

    /// <summary>The viewer's own country, for weighting a diaspora community match.</summary>
    public string? ViewerCountryCode { get; init; }

    /// <summary>The free-text term, when one was supplied.</summary>
    public string? Query { get; init; }
}
