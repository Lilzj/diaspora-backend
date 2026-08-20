namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Replaces a learning path's whole sequence, in the order given.
/// <para>
/// Allowed on a published path, deliberately. The alternative — unpublish, edit, republish — takes
/// a country's onboarding offline for as long as the editor takes, and the commonest edit by far is
/// inserting a step somebody discovered was missing.
/// </para>
/// </summary>
public sealed record ArrangeLearningPathRequest
{
    /// <summary>The steps, in order. An empty list clears the path and makes it unpublishable.</summary>
    public IReadOnlyList<LearningPathStepInput> Steps { get; init; } = [];

    /// <summary>The revision the client was looking at.</summary>
    public int? Revision { get; init; }
}
