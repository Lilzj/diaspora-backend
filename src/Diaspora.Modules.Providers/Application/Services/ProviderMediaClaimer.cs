using Diaspora.Modules.Media.Contracts.V1.Requests;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Providers.Observability;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Providers.Application.Services;

/// <summary>
/// Tells Media that a provider has claimed its uploads.
/// <para>
/// <b>Not optional, and this is the reason.</b> Media sweeps assets nothing ever attached, so a
/// provider created from four uploaded photographs whose assets were never claimed loses its
/// images a day later — silently, because every upload succeeded. Claiming is what takes them
/// out of the orphan population.
/// </para>
/// <para>
/// Isolated in this one class so exactly one file in B7 knows how to talk to Media. B7 consumes
/// Media's frozen <c>V1</c> request contract in-process rather than over HTTP; the module
/// boundary is respected because nothing of Media's internals is referenced, only its published
/// DTO and the owner-type enum its own freeze publishes for verticals —
/// <c>MediaOwnerType.Provider</c>, which has existed since B5 and needed no change.
/// </para>
/// <para>
/// <b>A failure here does not fail the write.</b> An unclaimed asset is a lost image, which is
/// bad; a refused profile edit because Media was briefly unavailable is worse. Failures are
/// logged and counted, and <c>MediaClaimFailures</c> is what makes a systematic problem visible
/// before a week of provider photographs has evaporated.
/// </para>
/// </summary>
public sealed class ProviderMediaClaimer
{
    private readonly ISender _sender;
    private readonly ILogger<ProviderMediaClaimer> _logger;

    public ProviderMediaClaimer(ISender sender, ILogger<ProviderMediaClaimer> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    /// <summary>
    /// Attaches an asset the caller is putting on a provider, and says whether Media agreed.
    /// <para>
    /// Unlike <see cref="ClaimAsync"/>, a refusal is returned, not logged: this is the member's
    /// own request, and Media's access check is what stops one member putting another's upload on
    /// their business (14 Oct 2026, <c>POST /providers/{id}/photos</c>).
    /// </para>
    /// </summary>
    public async Task<Result> AttachAsync(
        Guid providerId,
        Guid mediaId,
        string role,
        int position,
        CancellationToken cancellationToken = default)
    {
        var attached = await _sender.Send(
            new Modules.Media.Application.Assets.AttachMediaCommand
            {
                Request = new AttachMediaRequest
                {
                    MediaId = mediaId,
                    OwnerType = nameof(MediaOwnerType.Provider),
                    OwnerId = providerId,
                    Role = role,
                    Position = position
                }
            },
            cancellationToken);

        return attached.IsFailure ? Result.Failure(attached.Error) : Result.Success();
    }

    public async Task ClaimAsync(
        Guid providerId,
        MediaId mediaId,
        string role,
        int position = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mediaId);

        try
        {
            await _sender.Send(
                new Modules.Media.Application.Assets.AttachMediaCommand
                {
                    Request = new AttachMediaRequest
                    {
                        MediaId = mediaId.Value,

                        // Media's own enum already carries a value for this module. B7 adds
                        // nothing to Media and edits nothing in it.
                        OwnerType = nameof(MediaOwnerType.Provider),
                        OwnerId = providerId,
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
#pragma warning disable CA1031 // A media claim must never fail a provider write. See the summary.
        catch (Exception exception)
        {
            ProviderTelemetry.MediaClaimFailures.Add(
                1, new KeyValuePair<string, object?>("role", role));

            _logger.LogWarning(
                exception,
                "Claiming media {MediaId} for provider {ProviderId} failed. The asset is at risk of being swept.",
                mediaId.Value,
                providerId);
        }
#pragma warning restore CA1031
    }
}
