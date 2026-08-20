using Diaspora.Modules.Media.Contracts.V1.Requests;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Modules.Community.Domain.Comments;
using Diaspora.Modules.Community.Domain.Posts;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Community.Application.Services;

/// <summary>
/// Tells Media that a post has claimed its uploads.
/// <para>
/// <b>Not optional, and this is the reason.</b> Media sweeps assets nothing ever attached, so a
/// post created from three uploaded photographs whose assets were never claimed loses its
/// images a day later — silently, because every upload succeeded. Claiming is what takes them
/// out of the orphan population.
/// </para>
/// <para>
/// Isolated in this one class so exactly one file in Community knows how to talk to Media.
/// Community consumes Media's frozen <c>V1</c> request contract in-process rather than over
/// HTTP; the module boundary is respected because nothing of Media's internals is referenced,
/// only its published DTO and the owner-type enum its own freeze publishes for verticals.
/// </para>
/// <para>
/// <b>A failure here does not fail the post.</b> An unclaimed asset is a lost image, which is
/// bad; a refused publish because Media was briefly unavailable is worse, and for an emergency
/// alert it is unacceptable. Failures are logged, and the sweeper's own orphan metric is what
/// makes a systematic problem visible.
/// </para>
/// </summary>
public sealed class MediaAttachmentClaimer
{
    private readonly ISender _sender;
    private readonly ILogger<MediaAttachmentClaimer> _logger;

    public MediaAttachmentClaimer(ISender sender, ILogger<MediaAttachmentClaimer> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task ClaimAsync(CommunityPost post, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(post);

        foreach (var attachment in post.Attachments)
        {
            if (attachment.MediaId is null)
            {
                continue;
            }

            await ClaimOneAsync(
                attachment.MediaId.Value,
                nameof(MediaOwnerType.CommunityUpdate),
                post.Id.Value,
                attachment.Role,
                attachment.Position,
                "post",
                cancellationToken);
        }
    }

    /// <summary>
    /// Claims a comment's single attachment.
    /// <para>
    /// <b>This existed as a defect for the whole life of the module, not as a missing feature.</b>
    /// A comment has carried an <c>AttachmentMediaId</c> since B6 and the handler that set it
    /// claimed nothing, because Media's enum had no <c>Comment</c> owner type to claim it under —
    /// so the asset stayed in the orphan population and the sweeper collected it a day later.
    /// Every upload succeeded and no test failed, which is exactly why it survived twelve phases.
    /// PSR-1 added the owner type and this method together; neither is useful without the other.
    /// </para>
    /// <para>
    /// One attachment, not a collection: a comment carries at most one image, which is a domain
    /// decision B6 made and this method simply honours.
    /// </para>
    /// </summary>
    public Task ClaimAsync(Comment comment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comment);

        return comment.AttachmentMediaId is { } mediaId
            ? ClaimOneAsync(
                mediaId.Value,
                nameof(MediaOwnerType.Comment),
                comment.Id.Value,
                MediaReferenceRoles.Attachment,
                position: 0,
                "comment",
                cancellationToken)
            : Task.CompletedTask;
    }

    /// <summary>
    /// The one place that talks to Media, shared by both claim paths so they cannot drift in how
    /// they fail.
    /// </summary>
    private async Task ClaimOneAsync(
        Guid mediaId,
        string ownerType,
        Guid ownerId,
        string role,
        int position,
        string ownerDescription,
        CancellationToken cancellationToken)
    {
        try
        {
            await _sender.Send(
                new Modules.Media.Application.Assets.AttachMediaCommand
                {
                    Request = new AttachMediaRequest
                    {
                        MediaId = mediaId,
                        OwnerType = ownerType,
                        OwnerId = ownerId,
                        Role = role,
                        Position = position
                    }
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A media claim must never fail a publish. See the summary.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Claiming media {MediaId} for {OwnerDescription} {OwnerId} failed. The asset is at risk of being swept.",
                mediaId,
                ownerDescription,
                ownerId);
        }
#pragma warning restore CA1031
    }
}
