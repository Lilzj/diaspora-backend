namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// One catalogued skill, for autocomplete and chip clouds.
/// <para>
/// The catalogue is an aid, not a gate: a posting or profile may name a skill that is not in it.
/// What this buys is the autocomplete that stops "React", "ReactJS" and "react.js" becoming three
/// skills in the first place.
/// </para>
/// </summary>
public sealed record JobSkillCatalogResponse
{
    /// <summary>The normalised match key both sides are keyed by.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    public string? CategoryKey { get; init; }

    /// <summary>How many postings and profiles name it. Drives the ordering.</summary>
    public required int UsageCount { get; init; }
}
