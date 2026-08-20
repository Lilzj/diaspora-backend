namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>
/// Attaches an asset to a provider.
/// <para>
/// A Media identifier and a role — never a URL (ADR-005). The upload happened against Media
/// first; this claims it, which is also what takes it out of the orphan population Media sweeps.
/// </para>
/// </summary>
public sealed record ProviderGalleryItemRequest
{
    public required Guid MediaId { get; init; }

    /// <summary><c>gallery</c>, <c>menu</c>, <c>price-list</c>, <c>brochure</c>, <c>certificate</c>.</summary>
    public string? Role { get; init; }

    public string? Caption { get; init; }
}
