namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// One step in a submitted sequence.
/// <para>
/// <b><see cref="StepId"/> is caller-supplied and that is load-bearing.</b> A client tracking "you
/// are on step 3" tracks it against this identifier, not against the position — because an editor
/// inserting a step above it must not silently advance every reader to the next one. Sending the
/// existing identifier for a step being kept is what preserves that; an empty value creates a new
/// step.
/// </para>
/// <para>
/// A step must name a guide, a knowledge category, or both. A step that names neither is a heading,
/// and a heading inside a numbered sequence is a step a reader cannot complete.
/// </para>
/// </summary>
public sealed record LearningPathStepInput
{
    /// <summary>The existing step's identifier, or empty to create one.</summary>
    public Guid StepId { get; init; }

    /// <summary>The step's own framing — not the guide's title.</summary>
    public required string Title { get; init; }

    public string? Description { get; init; }

    /// <summary>The guide to start with.</summary>
    public Guid? GuideId { get; init; }

    /// <summary>The subject to explore, for a step no single guide answers.</summary>
    public string? KnowledgeCategoryKey { get; init; }

    /// <summary>Whether the step counts towards completion.</summary>
    public bool IsOptional { get; init; }
}
