namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>
/// Replaces the guides an organization points its readers at.
/// <para>
/// <b>Unknown and unpublished guides are dropped, not refused.</b> That is
/// <see cref="Application.Services.CommunityLifeGuideResolver"/>'s documented rule and it is right —
/// a guide that was unpublished should stop being offered, and refusing the whole write over one
/// stale reference would be disproportionate. It does mean a caller cannot tell from a 200 that
/// every identifier it sent was attached, so the response carries the resolved list and the caller
/// should read it rather than assume.
/// </para>
/// </summary>
public sealed record SetOrganizationGuidesRequest
{
    /// <summary>
    /// The guides to attach, in no particular order. An empty list clears them.
    /// </summary>
    public IReadOnlyList<Guid> GuideIds { get; init; } = [];
}
