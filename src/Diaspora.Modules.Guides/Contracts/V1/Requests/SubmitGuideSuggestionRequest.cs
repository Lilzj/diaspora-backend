namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Proposes an improvement to somebody else's guide — the platform's lightweight merge request.
/// Accepting one does not auto-edit the guide; the author applies it through the ordinary
/// editor, where it gets the same validation and version history as any edit.
/// </summary>
public sealed record SubmitGuideSuggestionRequest
{
    /// <summary>The section it targets, when it targets one. Null proposes to the whole guide.</summary>
    public Guid? TargetSectionId { get; init; }

    public required string ProposedContent { get; init; }

    /// <summary>Why the change should be made, in the suggester's words.</summary>
    public string? Rationale { get; init; }
}
