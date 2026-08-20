namespace Diaspora.Modules.Media.Contracts.V1.Requests;

/// <summary>
/// Claims an uploaded asset for an owning entity.
/// <para>
/// The step that takes an asset out of the orphan population. A vertical that uploads a
/// photograph and never attaches it finds out within a day, because the sweeper removes it and
/// says so in a metric — rather than never finding out, which is what every hand-rolled upload
/// endpoint eventually discovers about its storage bill.
/// </para>
/// </summary>
public sealed record AttachMediaRequest
{
    public required Guid MediaId { get; init; }

    /// <summary><c>userProfile</c>, <c>housingListing</c>, <c>message</c>, and so on.</summary>
    public required string OwnerType { get; init; }

    public required Guid OwnerId { get; init; }

    /// <summary><c>cover</c>, <c>gallery</c>, <c>avatar</c>, <c>logo</c>, <c>document</c>.</summary>
    public required string Role { get; init; }

    /// <summary>Order within the role. Zero when the role holds a single asset.</summary>
    public int Position { get; init; }
}
