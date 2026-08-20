namespace Diaspora.Modules.Media.Contracts.V1.Responses;

/// <summary>
/// What an asset is to the thing that owns it.
/// <para>
/// The shape every business module stores in place of a URL: an identifier, an owner and a
/// role. A housing listing holds one reference with role <c>cover</c> and several with role
/// <c>gallery</c>, ordered by <see cref="Position"/> — and holds no idea where the bytes are.
/// </para>
/// </summary>
public sealed record MediaReferenceResponse
{
    public required Guid MediaId { get; init; }

    /// <summary><c>userProfile</c>, <c>housingListing</c>, <c>message</c>, and so on.</summary>
    public required string OwnerType { get; init; }

    public required Guid OwnerId { get; init; }

    /// <summary><c>cover</c>, <c>gallery</c>, <c>avatar</c>, <c>logo</c>, <c>document</c>.</summary>
    public required string Role { get; init; }

    /// <summary>Order within the role. Zero when the role holds one asset.</summary>
    public required int Position { get; init; }
}
