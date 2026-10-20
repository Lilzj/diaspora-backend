namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Makes one of the member's own image uploads their profile photo. The upload is made first,
/// through <c>POST /api/v1/media/uploads/direct</c>; this names it.
/// </summary>
public sealed record SetAvatarRequest
{
    public required Guid MediaId { get; init; }
}
