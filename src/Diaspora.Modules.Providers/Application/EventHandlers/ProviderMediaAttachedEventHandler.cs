using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Domain.Events;

namespace Diaspora.Modules.Providers.Application.EventHandlers;

/// <summary>
/// Claims an attached asset with Media.
/// <para>
/// <b>This handler is the difference between a provider's photographs existing tomorrow and
/// not.</b> B5 sweeps assets nothing ever attached, so a logo uploaded and referenced but never
/// claimed disappears within a day — silently, because every upload reported success.
/// </para>
/// <para>
/// Deliberately a domain-event handler rather than an inline call in the write handler, so the
/// claim happens <em>outside</em> the write transaction: a Media outage then costs a lost image
/// rather than a refused profile edit. The claimer already swallows and counts its own failures.
/// </para>
/// </summary>
public sealed class ProviderMediaAttachedEventHandler
    : DomainEventHandler<ProviderMediaAttachedDomainEvent>
{
    private readonly ProviderMediaClaimer _claimer;

    public ProviderMediaAttachedEventHandler(ProviderMediaClaimer claimer) => _claimer = claimer;

    protected override Task HandleAsync(
        ProviderMediaAttachedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _claimer.ClaimAsync(
            domainEvent.ProviderId.Value,
            domainEvent.MediaId,
            domainEvent.Role,
            domainEvent.Position,
            cancellationToken);
    }
}
