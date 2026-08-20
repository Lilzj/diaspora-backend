namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>One community improvement proposal, with its decision when there is one.</summary>
public sealed record GuideSuggestionResponse
{
    public required Guid SuggestionId { get; init; }

    public required Guid GuideId { get; init; }

    /// <summary>The suggester — never the guide's author.</summary>
    public required Guid AuthorId { get; init; }

    /// <summary>The section it targets. Null proposes to the whole guide.</summary>
    public Guid? TargetSectionId { get; init; }

    public required string ProposedContent { get; init; }

    public string? Rationale { get; init; }

    /// <summary>A <c>GuideSuggestionStatus</c> name — <c>Open</c>, <c>Accepted</c>, <c>Rejected</c>, <c>Withdrawn</c>.</summary>
    public required string Status { get; init; }

    public Guid? DecidedBy { get; init; }

    public DateTimeOffset? DecidedAt { get; init; }

    /// <summary>What the decider told the suggester. Shown to both.</summary>
    public string? DecisionNote { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
