using Diaspora.Modules.Media.Contracts.V1.Requests;
using Diaspora.Modules.Media.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.CommunityLife.Application.Services;

/// <summary>
/// Tells Media that an event or an organization has claimed its uploads.
/// <para>
/// <b>Not optional, and this is the reason.</b> Media sweeps assets nothing ever attached, so an
/// event whose banner was uploaded and never claimed loses it a day later — silently, because every
/// upload succeeded and every attach succeeded. An event with no image is an event nobody opens.
/// </para>
/// <para>
/// Isolated in this one class so exactly one file in B12 knows how to talk to Media. Both
/// <c>MediaOwnerType.Event</c> and <c>MediaOwnerType.Organization</c> have existed in Media's own
/// enum since B5, so B12 adds nothing to Media and edits nothing in it.
/// </para>
/// <para>
/// <b>A failure here does not fail the write.</b> An unclaimed asset is a lost photograph, which is
/// bad; a refused event because Media was briefly unavailable is worse.
/// </para>
/// </summary>
public sealed class CommunityLifeMediaClaimer
{
    private readonly ISender _sender;
    private readonly ILogger<CommunityLifeMediaClaimer> _logger;

    public CommunityLifeMediaClaimer(ISender sender, ILogger<CommunityLifeMediaClaimer> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    /// <summary>Claims one asset for an event.</summary>
    public Task ClaimForEventAsync(
        Guid eventId,
        Guid mediaId,
        string? role,
        int position = 0,
        CancellationToken cancellationToken = default) =>
        ClaimAsync(nameof(MediaOwnerType.Event), eventId, mediaId, role, position, cancellationToken);

    /// <summary>Claims one asset for an organization.</summary>
    public Task ClaimForOrganizationAsync(
        Guid organizationId,
        Guid mediaId,
        string? role,
        int position = 0,
        CancellationToken cancellationToken = default) =>
        ClaimAsync(
            nameof(MediaOwnerType.Organization),
            organizationId,
            mediaId,
            role,
            position,
            cancellationToken);

    /// <summary>
    /// Claims several assets for one subject.
    /// <para>
    /// Sequential rather than parallel, deliberately: the sender resolves scoped handlers over a
    /// shared DbContext, and firing these concurrently is a concurrent use of one context that EF
    /// refuses. An event has at most forty images; the round trips are cheap.
    /// </para>
    /// </summary>
    public async Task ClaimManyAsync(
        string ownerType,
        Guid ownerId,
        IReadOnlyList<(Guid MediaId, string? Role)> media,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(media);

        for (var index = 0; index < media.Count; index++)
        {
            await ClaimAsync(
                ownerType, ownerId, media[index].MediaId, media[index].Role, index,
                cancellationToken);
        }
    }

    /// <summary>
    /// Claims several assets for an event.
    /// <para>
    /// Typed so no handler has to name Media's owner-type vocabulary. A misspelt string here would
    /// leave the assets unclaimed and swept away days later, with the event's gallery going blank
    /// long after anybody would connect the two.
    /// </para>
    /// </summary>
    public Task ClaimManyForEventAsync(
        Guid eventId,
        IReadOnlyList<(Guid MediaId, string? Role)> media,
        CancellationToken cancellationToken = default) =>
        ClaimManyAsync(nameof(MediaOwnerType.Event), eventId, media, cancellationToken);

    /// <summary>Claims several assets for an organization. See <see cref="ClaimManyForEventAsync"/>.</summary>
    public Task ClaimManyForOrganizationAsync(
        Guid organizationId,
        IReadOnlyList<(Guid MediaId, string? Role)> media,
        CancellationToken cancellationToken = default) =>
        ClaimManyAsync(
            nameof(MediaOwnerType.Organization), organizationId, media, cancellationToken);

    private async Task ClaimAsync(
        string ownerType,
        Guid ownerId,
        Guid mediaId,
        string? role,
        int position,
        CancellationToken cancellationToken)
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

                        // Media's own enum already carries a value for both of this module's
                        // subjects, so B12 edits nothing in B5.
                        OwnerType = ownerType,
                        OwnerId = ownerId,
                        Role = role ?? nameof(Domain.Enums.CommunityLifeMediaRole.Gallery),
                        Position = position
                    }
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A media claim must never fail a write. See the class summary.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Claiming media {MediaId} for {OwnerType} {OwnerId} failed. The asset is at risk of being swept.",
                mediaId,
                ownerType,
                ownerId);
        }
#pragma warning restore CA1031
    }
}
