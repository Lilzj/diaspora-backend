using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Catalog;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Events;
using Diaspora.Modules.CommunityLife.Observability;

namespace Diaspora.Modules.CommunityLife.Application.EventHandlers;

/// <summary>
/// An event was called off. Publish it, and give the category its place back.
/// <para>
/// <b>The category count is only released if the event ever reached the catalogue.</b> A draft
/// abandoned before publication never took a place, and decrementing for it would walk every
/// category's count downward over a year of abandoned drafts.
/// </para>
/// <para>
/// The affected-attendee figure travels on the domain event rather than being counted here, because
/// the responses are released by the same command that cancelled — by the time this runs the count
/// would already be zero.
/// </para>
/// </summary>
public sealed class CommunityEventCancelledEventHandler
    : DomainEventHandler<CommunityEventCancelledDomainEvent>
{
    private readonly CommunityLifeIntegrationEventRelay _relay;
    private readonly ICommunityEventRepository _events;
    private readonly IEventCategoryRepository _categories;

    public CommunityEventCancelledEventHandler(
        CommunityLifeIntegrationEventRelay relay,
        ICommunityEventRepository events,
        IEventCategoryRepository categories)
    {
        _relay = relay;
        _events = events;
        _categories = categories;
    }

    protected override async Task HandleAsync(
        CommunityEventCancelledDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        var communityEvent = await _events.GetAsync(
            domainEvent.CommunityEventId, cancellationToken);

        if (communityEvent?.FirstPublishedAt is not null)
        {
            var category = await _categories.FindByKeyAsync(
                communityEvent.CategoryKey, cancellationToken);

            category?.AdjustCount(-1, domainEvent.CancelledAt);
        }

        CommunityLifeTelemetry.EventsCancelled.Add(
            1,
            new KeyValuePair<string, object?>(
                "affected", domainEvent.AffectedAttendeeCount > 0));
    }
}
