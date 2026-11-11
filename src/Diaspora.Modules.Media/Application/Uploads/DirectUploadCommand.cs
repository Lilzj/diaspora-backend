using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Contracts.V1.Responses;

namespace Diaspora.Modules.Media.Application.Uploads;

/// <summary>
/// The whole upload in one request.
/// <para>
/// The path almost every upload in this app actually takes: an <c>expo-image-picker</c> result
/// at <c>quality: 0.8</c> is well under a megabyte, and making a client open a session, send
/// one part and complete it would be three round trips to do one thing.
/// </para>
/// <para>
/// It shares every validation, hash and processing step with the chunked path. The only
/// difference is that no session is persisted, so there is nothing to resume and nothing to
/// sweep.
/// </para>
/// </summary>
[Auditable]
[Transactional(Reason =
    "A direct upload writes the asset and its processing job together. Either both land or " +
    "the stored object is an orphan the sweeper collects.")]
[RequirePolicy(MediaPolicies.UploadImage)]
public sealed record DirectUploadCommand : ICommand<MediaAssetResponse>
{
    public required string Kind { get; init; }

    public required string ContentType { get; init; }

    public required string FileName { get; init; }

    public required Stream Content { get; init; }

    public string? OwnerType { get; init; }

    public Guid? OwnerId { get; init; }

    public string? Role { get; init; }
}
