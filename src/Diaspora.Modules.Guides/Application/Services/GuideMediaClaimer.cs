using Diaspora.Modules.Media.Contracts.V1.Requests;
using Diaspora.Modules.Media.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Guides.Application.Services;

/// <summary>
/// Tells Media that a guide has claimed its uploads.
/// <para>
/// <b>Not optional, and this is the reason.</b> Media sweeps assets nothing ever attached, so
/// a guide illustrated with four uploaded screenshots whose assets were never claimed loses
/// its images a day later — silently, because every upload succeeded. Claiming is what takes
/// them out of the orphan population.
/// </para>
/// <para>
/// Isolated in this one class so exactly one file in B8 knows how to talk to Media. B8
/// consumes Media's frozen <c>V1</c> request contract in-process; <c>MediaOwnerType.Guide</c>
/// has existed in Media's own enum since B5, so B8 adds nothing to Media and edits nothing
/// in it.
/// </para>
/// <para>
/// <b>A failure here does not fail the write.</b> An unclaimed asset is a lost image, which is
/// bad; a refused guide save because Media was briefly unavailable is worse. Failures are
/// logged and the committed guide write stands.
/// </para>
/// </summary>
public sealed class GuideMediaClaimer
{
    private readonly ISender _sender;
    private readonly ILogger<GuideMediaClaimer> _logger;

    public GuideMediaClaimer(ISender sender, ILogger<GuideMediaClaimer> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task ClaimAsync(
        Guid guideId,
        Guid mediaId,
        string role,
        int position = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _sender.Send(
                new Modules.Media.Application.Assets.AttachMediaCommand
                {
                    Request = new AttachMediaRequest
                    {
                        MediaId = mediaId,

                        // Media's own enum already carries a value for this module. B8 adds
                        // nothing to Media and edits nothing in it.
                        OwnerType = nameof(MediaOwnerType.Guide),
                        OwnerId = guideId,
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
#pragma warning disable CA1031 // A media claim must never fail a guide write. See the summary.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Claiming media {MediaId} for guide {GuideId} failed. The asset is at risk of being swept.",
                mediaId,
                guideId);
        }
#pragma warning restore CA1031
    }
}
