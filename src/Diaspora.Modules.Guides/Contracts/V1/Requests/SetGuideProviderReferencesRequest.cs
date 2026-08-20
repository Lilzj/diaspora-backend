namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Replaces the guide's provider references wholesale. Each provider is validated to exist
/// through B7's directory before it is written — a guide must not recommend a ghost.
/// </summary>
public sealed record SetGuideProviderReferencesRequest
{
    public IReadOnlyList<GuideProviderReferenceInput> References { get; init; } = [];
}
