namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>Accepts or rejects an open suggestion. The note is shown to the suggester.</summary>
public sealed record DecideGuideSuggestionRequest
{
    public required bool Accept { get; init; }

    public string? Note { get; init; }
}
