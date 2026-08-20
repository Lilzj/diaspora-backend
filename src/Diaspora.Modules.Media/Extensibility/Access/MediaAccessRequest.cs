using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Media.Extensibility.Access;

/// <summary>
/// What a vertical is asked to judge: who, over which of its entities, wanting to do what.
/// <para>
/// Carries the owning entity's identifier rather than the asset's, because that is the
/// identifier the vertical can act on. Housing has a table of listings, not a table of
/// media — and asking it about a media id would force it to store one.
/// </para>
/// </summary>
public sealed record MediaAccessRequest
{
    /// <summary>The caller. Null when anonymous.</summary>
    public UserId? Caller { get; init; }

    /// <summary>The asset in question, for logging and for the rare policy that needs it.</summary>
    public required MediaId MediaId { get; init; }

    public required MediaOwnerType OwnerType { get; init; }

    /// <summary>The vertical's own entity: the listing, the message, the organisation.</summary>
    public required Guid OwnerId { get; init; }

    /// <summary>What the asset is to its owner: <c>cover</c>, <c>gallery</c>, <c>floor-plan</c>.</summary>
    public string? Role { get; init; }

    public required MediaAccessLevel RequestedLevel { get; init; }

    /// <summary>The member who uploaded the asset.</summary>
    public required UserId UploadedBy { get; init; }
}
