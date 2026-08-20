using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Domain.Events;

namespace Diaspora.Modules.Jobs.Application.EventHandlers;

/// <summary>
/// Claims an attached asset in Media.
/// <para>
/// <b>Not optional.</b> Media sweeps assets nothing ever attached, so a posting illustrated with
/// an uploaded workplace photo whose asset was never claimed loses it a day later — silently,
/// because both the upload and the attach succeeded. This handler is what takes it out of the
/// orphan population.
/// </para>
/// </summary>
public sealed class JobPostingMediaAttachedEventHandler
    : DomainEventHandler<JobPostingMediaAttachedDomainEvent>
{
    private readonly JobMediaClaimer _media;

    public JobPostingMediaAttachedEventHandler(JobMediaClaimer media) => _media = media;

    protected override async Task HandleAsync(
        JobPostingMediaAttachedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await _media.ClaimAsync(
            domainEvent.PostingId.Value,
            domainEvent.MediaId,
            JobWireNames.ToWire(domainEvent.Role),
            position: 0,
            cancellationToken);
    }
}
