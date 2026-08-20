namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// One asset attached to a provider.
/// <para>
/// <b>An identifier and a role — never a URL</b> (ADR-005). The client resolves the identifier
/// through Media's own link endpoint, which is what makes a storage-provider migration invisible
/// to this contract and to every client that shipped against it.
/// </para>
/// <para>
/// Shaped to match B5's published <c>MediaReferenceResponse</c> rather than reusing that type,
/// so a client parsing a provider does not have to reference Media's contract assembly to read a
/// field it already understands.
/// </para>
/// </summary>
public sealed record ProviderMediaResponse
{
    public required Guid MediaId { get; init; }

    /// <summary><c>cover</c>, <c>logo</c>, <c>gallery</c>, <c>menu</c>, <c>certificate</c>.</summary>
    public required string Role { get; init; }

    /// <summary>Order within the role. Zero when the role holds a single asset.</summary>
    public required int Position { get; init; }

    public string? Caption { get; init; }

    /// <summary>False while the asset is still at risk of being swept by Media.</summary>
    public required bool IsClaimed { get; init; }
}
