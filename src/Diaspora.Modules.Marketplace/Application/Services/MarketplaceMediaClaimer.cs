using Diaspora.Modules.Media.Contracts.V1.Requests;
using Diaspora.Modules.Media.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Marketplace.Application.Services;

/// <summary>
/// Tells Media that a listing has claimed its uploads.
/// <para>
/// <b>Not optional, and this is the reason.</b> Media sweeps assets nothing ever attached, so a
/// listing whose photographs were uploaded and never claimed loses them a day later — silently,
/// because every upload succeeded and every attach succeeded. On a marketplace that is the
/// difference between a listing that sells and one nobody opens, because a listing with no image is
/// a listing nobody trusts.
/// </para>
/// <para>
/// Isolated in this one class so exactly one file in B10 knows how to talk to Media. B10 consumes
/// Media's frozen <c>V1</c> request contract in-process; <c>MediaOwnerType.MarketplaceListing</c>
/// has existed in Media's own enum since B5, so B10 adds nothing to Media and edits nothing in it.
/// </para>
/// <para>
/// <b>A failure here does not fail the write.</b> An unclaimed asset is a lost photograph, which is
/// bad; a refused listing because Media was briefly unavailable is worse. Failures are logged and
/// the committed write stands.
/// </para>
/// </summary>
public sealed class MarketplaceMediaClaimer
{
    private readonly ISender _sender;
    private readonly ILogger<MarketplaceMediaClaimer> _logger;

    public MarketplaceMediaClaimer(ISender sender, ILogger<MarketplaceMediaClaimer> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    /// <summary>Claims one asset for a listing.</summary>
    public async Task ClaimAsync(
        Guid ownerId,
        Guid mediaId,
        string role,
        int position = 0,
        CancellationToken cancellationToken = default)
    {
        if (mediaId == Guid.Empty || ownerId == Guid.Empty)
        {
            return;
        }

        try
        {
            await _sender.Send(
                new Modules.Media.Application.Assets.AttachMediaCommand
                {
                    Request = new AttachMediaRequest
                    {
                        MediaId = mediaId,

                        // Media's own enum already carries a value for this module. B10 adds
                        // nothing to Media and edits nothing in it.
                        OwnerType = nameof(MediaOwnerType.MarketplaceListing),
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
#pragma warning disable CA1031 // A media claim must never fail a listing write. See the summary.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Claiming media {MediaId} for listing {OwnerId} failed. The asset is at risk of being swept.",
                mediaId,
                ownerId);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Claims several assets for one listing.
    /// <para>
    /// Sequential rather than parallel, deliberately: the sender resolves scoped handlers over a
    /// shared DbContext, and firing these concurrently is a concurrent use of one context that EF
    /// refuses. A listing has at most two dozen photographs; the round trips are cheap.
    /// </para>
    /// </summary>
    public async Task ClaimManyAsync(
        Guid ownerId,
        IReadOnlyList<(Guid MediaId, string Role)> assets,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assets);

        for (var index = 0; index < assets.Count; index++)
        {
            await ClaimAsync(
                ownerId, assets[index].MediaId, assets[index].Role, index, cancellationToken);
        }
    }
}
