namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Opens a curated onboarding route for a country.
/// <para>
/// The country comes from the route. The key is unique within it, so <c>arriving</c> is the natural
/// key for the arrival path of every country and no curator has to invent <c>arriving-mu</c>.
/// </para>
/// <para>
/// Created as a draft with no steps. Arranging and publishing are separate acts, because a path is
/// written over days and must not be half-visible while it is.
/// </para>
/// </summary>
public sealed record CreateLearningPathRequest
{
    /// <summary>Lower-case kebab. Unique within the country.</summary>
    public required string Key { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    /// <summary>The language this path's own copy is written in. A second language is a second path.</summary>
    public string? Language { get; init; }
}
