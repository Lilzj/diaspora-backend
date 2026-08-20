using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Domain.Events;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Relays a language change.
/// <para>
/// Its own contract for the same reason recategorisation is: language decides which analyser a
/// search index uses and which audience a notification reaches, so a consumer needs both the
/// previous code and the current one to move the document rather than reindex it blindly.
/// </para>
/// </summary>
public sealed class GuideLanguageChangedEventHandler
    : DomainEventHandler<GuideLanguageChangedDomainEvent>
{
    private readonly GuideIntegrationEventRelay _relay;

    public GuideLanguageChangedEventHandler(GuideIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        GuideLanguageChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
