namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// One provider the guide points readers at. An identifier and editorial free text — the
/// provider's name, badge and slug are hydrated at read time through B7's directory, never
/// copied here.
/// </summary>
public sealed record GuideProviderReferenceInput
{
    public required Guid ProviderId { get; init; }

    /// <summary>What the guide calls this provider — "Recommended lawyer". Editorial.</summary>
    public string? Role { get; init; }

    /// <summary>Why the guide recommends them, in the author's words.</summary>
    public string? Note { get; init; }
}
