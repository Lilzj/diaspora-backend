namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>
/// Gives a listing extra placement for a window.
/// <para>
/// <b>Nothing here charges anybody.</b> Payments are out of scope for B10; a promotion is granted
/// by staff today and by a billing module later, and <see cref="ExternalReference"/> is where that
/// module's receipt identifier lands without a schema change.
/// </para>
/// </summary>
public sealed record CreatePromotionRequest
{
    /// <summary>
    /// <c>Featured</c>, <c>Bump</c>, <c>Spotlight</c> or <c>CategoryHighlight</c>. Each lifts one
    /// listing; none of them pushes another down.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>When it starts. Now when omitted.</summary>
    public DateTimeOffset? StartsAt { get; init; }

    public required DateTimeOffset EndsAt { get; init; }

    /// <summary>A receipt or order identifier from whatever granted this. Unused today.</summary>
    public string? ExternalReference { get; init; }
}
