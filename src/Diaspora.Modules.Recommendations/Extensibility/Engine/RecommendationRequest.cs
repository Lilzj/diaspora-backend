using Diaspora.Contracts.Authorization;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Extensibility.Engine;

/// <summary>
/// One ask of the engine.
/// <para>
/// <b>The surface is required and the target type is not, and that asymmetry is the whole model.</b>
/// A surface always exists — a page is being built for somewhere — and it determines which strategies
/// run, at what weights, under which policies. A target type is a narrowing the jobs screen applies
/// and the home feed does not.
/// </para>
/// </summary>
public sealed record RecommendationRequest
{
    public required UserId UserId { get; init; }

    public required RecommendationSurface Surface { get; init; }

    /// <summary>Null asks for whatever the surface holds. The home feed's case.</summary>
    public RecommendationTargetType? TargetType { get; init; }

    /// <summary>
    /// Several types at once, for a surface that mixes a known few.
    /// <para>
    /// Distinct from <see cref="TargetType"/> rather than folded into a list of one, because a
    /// request for exactly one type is stored on the generated set and a request for three is not.
    /// </para>
    /// </summary>
    public IReadOnlyList<RecommendationTargetType> TargetTypes { get; init; } = [];

    public int Limit { get; init; } = 20;

    /// <summary>The caller's tier. Passed through — this module does not resolve identity.</summary>
    public required AccessTier AccessTier { get; init; }

    /// <summary>Overrides the member's country. The "show me Canada" case on a country page.</summary>
    public string? CountryCodeOverride { get; init; }

    /// <summary>Narrows to categories. Empty accepts every category the surface holds.</summary>
    public IReadOnlyList<string> CategoryKeys { get; init; } = [];

    /// <summary>
    /// Ignores a fresh stored set and ranks again.
    /// <para>
    /// What a pull-to-refresh means. Deliberately not the default: three refreshes in ten seconds
    /// should not be three ranking passes.
    /// </para>
    /// </summary>
    public bool ForceRegenerate { get; init; }

    /// <summary>
    /// Skips persisting the result.
    /// <para>
    /// For the search-boost path, which asks the engine for scores rather than for a page. Persisting
    /// those would fill the set table with pages nobody was ever shown and would make every
    /// acceptance rate meaningless.
    /// </para>
    /// </summary>
    public bool Transient { get; init; }
}
