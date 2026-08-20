namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>
/// Edits a provider's identity and classification.
/// <para>
/// <b>The slug is not here.</b> A rename must not silently change the deep-link handle: every
/// printed flyer, WhatsApp share and search result pointing at the old one would break, and the
/// person renaming their business has no idea that is what they did. Changing it is its own
/// endpoint and its own decision.
/// </para>
/// </summary>
public sealed record UpdateProviderRequest
{
    public required string Name { get; init; }

    /// <summary>The <c>ProviderType</c> name.</summary>
    public required string ProviderType { get; init; }

    /// <summary>An identifier, slug, name or alias. Resolved through the catalogue.</summary>
    public required string Category { get; init; }

    /// <summary>The <c>ServiceDeliveryMode</c> name.</summary>
    public required string DeliveryMode { get; init; }

    public IReadOnlyList<string>? Tags { get; init; }
}
