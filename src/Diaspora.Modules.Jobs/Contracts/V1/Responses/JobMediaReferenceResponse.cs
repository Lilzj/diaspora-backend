namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// One media asset attached to a posting, by reference.
/// <para>
/// A <c>MediaId</c> and never a URL. The client asks Media for the link, which is minted against
/// the reader — so an asset cannot leak to someone who may not read it.
/// </para>
/// </summary>
public sealed record JobMediaReferenceResponse
{
    public required Guid MediaId { get; init; }

    /// <summary><c>Logo</c>, <c>Cover</c>, <c>WorkplacePhoto</c>, <c>Document</c>, …</summary>
    public required string Role { get; init; }

    public string? Caption { get; init; }
}
