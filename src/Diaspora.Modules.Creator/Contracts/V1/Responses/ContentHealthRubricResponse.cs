using Diaspora.Modules.Creator.Domain.ContentHealth;

namespace Diaspora.Modules.Creator.Contracts.V1.Responses;

/// <summary>
/// The platform's definition of content health, on the wire — BG-08.
/// <para>
/// Served rather than computed, for the reason on <see cref="ContentHealthRubric"/>: the Creator
/// module may not read the four content modules whose items would be scored, so the rule ships from
/// the server and the client applies it to the <c>/mine</c> payloads it already holds.
/// </para>
/// </summary>
public sealed record ContentHealthRubricResponse
{
    /// <summary>Bumped when a criterion or weight changes. A score is only comparable within one.</summary>
    public required int Version { get; init; }

    /// <summary>At or above this percentage, an item is healthy.</summary>
    public required int HealthyAtOrAbove { get; init; }

    /// <summary>Below this percentage, an item needs work. Between the two it is fair.</summary>
    public required int NeedsWorkBelow { get; init; }

    public required IReadOnlyList<ContentHealthCriterionResponse> Criteria { get; init; }

    /// <summary>The rubric as it stands. Static: it is a definition, not a query.</summary>
    public static ContentHealthRubricResponse Current { get; } = new()
    {
        Version = ContentHealthRubric.Version,
        HealthyAtOrAbove = ContentHealthRubric.HealthyAtOrAbove,
        NeedsWorkBelow = ContentHealthRubric.NeedsWorkBelow,
        Criteria =
        [
            .. ContentHealthRubric.Criteria.Select(criterion => new ContentHealthCriterionResponse
            {
                Key = criterion.Key,
                Field = criterion.Field,
                Weight = criterion.Weight,
                Hint = criterion.Hint,
                AppliesTo = criterion.AppliesTo,
            })
        ],
    };
}

/// <summary>One thing an item either has or has not.</summary>
public sealed record ContentHealthCriterionResponse
{
    public required string Key { get; init; }

    /// <summary>The field on the content item that satisfies it.</summary>
    public required string Field { get; init; }

    /// <summary>Relative weight. Scores are normalised over the criteria that apply.</summary>
    public required int Weight { get; init; }

    /// <summary>What the member should do about it. Rendered verbatim.</summary>
    public required string Hint { get; init; }

    /// <summary>Content types this applies to. Empty means all of them.</summary>
    public required IReadOnlyList<string> AppliesTo { get; init; }
}
