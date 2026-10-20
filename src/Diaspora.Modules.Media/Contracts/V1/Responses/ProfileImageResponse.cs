namespace Diaspora.Modules.Media.Contracts.V1.Responses;

/// <summary>
/// A member's own image, made public for their profile: the asset and the address anyone can
/// load it from.
/// </summary>
public sealed record ProfileImageResponse
{
    public required Guid MediaId { get; init; }

    /// <summary><c>avatar</c> or <c>cover</c>.</summary>
    public required string Role { get; init; }

    /// <summary>
    /// The unsigned <c>medium</c> rendition — absolute, cacheable, and readable without a bearer
    /// token. A rendition not produced yet is served as the original by the same route.
    /// </summary>
    public required string Url { get; init; }
}
