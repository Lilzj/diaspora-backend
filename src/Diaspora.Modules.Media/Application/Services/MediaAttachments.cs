using Diaspora.Modules.Media.Application.Assets;
using Diaspora.Modules.Media.Contracts.V1.Requests;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Integration;
using Diaspora.SharedKernel.Results;
using MediatR;

namespace Diaspora.Modules.Media.Application.Services;

/// <summary>
/// Constructs B5's own command envelopes on B5's side of the seam.
/// <para>
/// Deliberately thin, and that is the whole point: the work was always B5's, and the only thing
/// that moves here is which assembly writes <c>new AttachMediaCommand</c>. Dispatching through
/// <see cref="ISender"/> rather than calling the handler keeps every pipeline behaviour —
/// authorisation, validation, auditing, the transaction — exactly where it was.
/// </para>
/// </summary>
public sealed class MediaAttachments : IMediaAttachments
{
    private readonly ISender _sender;

    public MediaAttachments(ISender sender) => _sender = sender;

    public Task<Result<MediaAssetResponse>> AttachAsync(
        AttachMediaRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _sender.Send(new AttachMediaCommand { Request = request }, cancellationToken);
    }

    public Task<Result<ProfileImageResponse>> PublishProfileImageAsync(
        Guid mediaId, string role, CancellationToken cancellationToken = default) =>
        _sender.Send(
            new PublishProfileImageCommand { MediaId = mediaId, Role = role }, cancellationToken);

    public Task<Result> WithdrawProfileImageAsync(
        Guid mediaId, string role, CancellationToken cancellationToken = default) =>
        _sender.Send(
            new WithdrawProfileImageCommand { MediaId = mediaId, Role = role }, cancellationToken);
}
