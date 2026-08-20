using Diaspora.Modules.Media.Contracts.V1.Requests;
using Diaspora.Modules.Media.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Jobs.Application.Services;

/// <summary>
/// Tells Media that a job has claimed its uploads.
/// <para>
/// <b>Not optional, and this is the reason.</b> Media sweeps assets nothing ever attached, so an
/// employer profile with a logo whose asset was never claimed loses it a day later — silently,
/// because every upload succeeded and every attach succeeded. Claiming is what takes an asset out
/// of the orphan population.
/// </para>
/// <para>
/// It matters more here than in most modules: a candidate's résumé and certificates are attached
/// to an application, and an application whose résumé was swept is one an employer cannot read
/// weeks after it was sent.
/// </para>
/// <para>
/// Isolated in this one class so exactly one file in B9 knows how to talk to Media. B9 consumes
/// Media's frozen <c>V1</c> request contract in-process; <c>MediaOwnerType.Job</c> has existed in
/// Media's own enum since B5, so B9 adds nothing to Media and edits nothing in it.
/// </para>
/// <para>
/// <b>A failure here does not fail the write.</b> An unclaimed asset is a lost document, which is
/// bad; a refused application because Media was briefly unavailable is worse — it costs somebody
/// a job application rather than an attachment. Failures are logged and the committed write
/// stands.
/// </para>
/// </summary>
public sealed class JobMediaClaimer
{
    private readonly ISender _sender;
    private readonly ILogger<JobMediaClaimer> _logger;

    public JobMediaClaimer(ISender sender, ILogger<JobMediaClaimer> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    /// <summary>Claims one asset for a job-owned entity.</summary>
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

                        // Media's own enum already carries a value for this module. B9 adds
                        // nothing to Media and edits nothing in it.
                        OwnerType = nameof(MediaOwnerType.Job),
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
#pragma warning disable CA1031 // A media claim must never fail a job write. See the summary.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Claiming media {MediaId} for job owner {OwnerId} failed. The asset is at risk of being swept.",
                mediaId,
                ownerId);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Claims several assets for one owner.
    /// <para>
    /// Sequential rather than parallel, deliberately: the sender resolves scoped handlers over a
    /// shared DbContext, and firing these concurrently is a concurrent use of one context that
    /// EF refuses. An application has at most ten attachments; the round trips are cheap.
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
