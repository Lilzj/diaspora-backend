namespace Diaspora.Modules.Recommendations.Contracts.V1.Requests;

/// <summary>
/// A change to one surface's ranking configuration.
/// <para>
/// <b>The most consequential request in the module.</b> One number here moves what an entire country
/// sees, which is why the endpoint behind it is staff-only, audited, and invalidates the strategy
/// cache so the change takes effect immediately rather than whenever an entry happened to expire.
/// </para>
/// <para>
/// Every property is nullable: an operator adjusting a weight should not have to restate the order,
/// the cold-start flag and every parameter, and a request shape that required it would make each
/// tuning change an opportunity to clear something by omission.
/// </para>
/// </summary>
public sealed record UpdateRankingStrategyRequest
{
    /// <summary>0–1.</summary>
    public decimal? Weight { get; init; }

    public int? Order { get; init; }

    public bool? IsEnabled { get; init; }

    public bool? RunsOnColdStart { get; init; }

    public decimal? MinimumScore { get; init; }

    public string? Description { get; init; }

    /// <summary>Parameters to set. Absent parameters are left as they are.</summary>
    public IReadOnlyDictionary<string, string>? Parameters { get; init; }
}
