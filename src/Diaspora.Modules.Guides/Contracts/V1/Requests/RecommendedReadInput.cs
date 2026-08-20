namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// One curated next read, on the way in. Order comes from the list, never from a field.
/// </summary>
public sealed record RecommendedReadInput
{
    public required Guid GuideId { get; init; }

    /// <summary>Why this comes next. The sentence a person wrote, and the whole point.</summary>
    public string? Reason { get; init; }
}
