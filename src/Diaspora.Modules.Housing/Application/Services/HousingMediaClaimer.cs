using Diaspora.Modules.Media.Contracts.V1.Requests;
using Diaspora.Modules.Media.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Housing.Application.Services;

/// <summary>
/// Tells Media that a property has claimed its uploads.
/// <para>
/// <b>Not optional, and this is the reason.</b> Media sweeps assets nothing ever attached, so a
/// listing whose photographs were uploaded and never claimed loses them a day later - silently,
/// because every upload succeeded and every attach succeeded. On a housing platform that is the
/// difference between a listing that lets and one nobody opens, because a property with no
/// photographs is a property members assume does not exist.
/// </para>
/// <para>
/// Isolated in this one class so exactly one file in B11 knows how to talk to Media.
/// <c>MediaOwnerType.HousingListing</c> has existed in Media own enum since B5, so B11 adds nothing
/// to Media and edits nothing in it.
/// </para>
/// <para>
/// <b>A failure here does not fail the write.</b> An unclaimed asset is a lost photograph, which is
/// bad; a refused listing because Media was briefly unavailable is worse.
/// </para>
/// </summary>
public sealed class HousingMediaClaimer
{
    private readonly ISender _sender;
    private readonly ILogger<HousingMediaClaimer> _logger;

    public HousingMediaClaimer(ISender sender, ILogger<HousingMediaClaimer> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    /// <summary>Claims one asset for a property.</summary>
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

                        // Media own enum already carries a value for this module.
                        OwnerType = nameof(MediaOwnerType.HousingListing),
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
    /// Claims several assets for one property.
    /// <para>
    /// Sequential rather than parallel, deliberately: the sender resolves scoped handlers over a
    /// shared DbContext, and firing these concurrently is a concurrent use of one context that EF
    /// refuses. A property has at most forty photographs; the round trips are cheap.
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
