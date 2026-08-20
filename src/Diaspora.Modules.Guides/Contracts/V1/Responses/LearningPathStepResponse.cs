namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One step of a curated onboarding sequence.
/// <para>
/// The step's own title and description, and — separately — the guide it points at. Both, because
/// they say different things: "Sort out your right to be here" is the step, and "Applying for an
/// occupation permit" is the guide. A client renders the first as the checklist line and the second
/// as the card beneath it.
/// </para>
/// <para>
/// <see cref="Guide"/> is null when the step names only a subject, and also when it names a guide
/// that has since been unpublished — the step survives, because a path that silently lost step 4
/// would leave every reader with a six-step sequence numbered 1 to 7.
/// </para>
/// </summary>
public sealed record LearningPathStepResponse
{
    /// <summary>Stable across rearrangements. What a client tracks progress against.</summary>
    public required Guid StepId { get; init; }

    public required int Position { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    /// <summary>Whether the step counts towards completion.</summary>
    public required bool IsOptional { get; init; }

    /// <summary>The subject to explore, when the step names one.</summary>
    public string? KnowledgeCategoryKey { get; init; }

    /// <summary>The guide to start with, resolved. Null when the step names none or it is gone.</summary>
    public GuideCardResponse? Guide { get; init; }
}
