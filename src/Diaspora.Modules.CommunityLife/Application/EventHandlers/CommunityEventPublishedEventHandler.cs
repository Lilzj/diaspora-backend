using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Catalog;
using Diaspora.Modules.CommunityLife.Domain.Events;
using Diaspora.Modules.CommunityLife.Domain.Organizations;

namespace Diaspora.Modules.CommunityLife.Application.EventHandlers;

/// <summary>
/// An event went live: publish the contract, credit the host body, and move the category's count.
/// <para>
/// <b>Only a first publication moves a counter.</b> An organizer who unpublishes to fix a venue and
/// publishes again has not run two events, and a body whose published count climbed every time
/// somebody corrected a typo would be a body nobody could rank by. <c>IsFirstPublication</c> is on
/// the domain event precisely so this decision does not need a read.
/// </para>
/// <para>
/// <b>Neither counter may fail the publication.</b> An event that is live beside a stale number is a
/// cosmetic problem; a publication rolled back because a category count would not move would lose
/// the organizer's work on the morning they needed it.
/// </para>
/// </summary>
public sealed class CommunityEventPublishedEventHandler
    : DomainEventHandler<CommunityEventPublishedDomainEvent>
{
    private readonly CommunityLifeIntegrationEventRelay _relay;
    private readonly IOrganizationRepository _organizations;
    private readonly IEventCategoryRepository _categories;

    public CommunityEventPublishedEventHandler(
        CommunityLifeIntegrationEventRelay relay,
        IOrganizationRepository organizations,
        IEventCategoryRepository categories)
    {
        _relay = relay;
        _organizations = organizations;
        _categories = categories;
    }

    protected override async Task HandleAsync(
        CommunityEventPublishedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        if (!domainEvent.IsFirstPublication)
        {
            return;
        }

        await CreditOrganizationAsync(domainEvent, cancellationToken);
        await CountCategoryAsync(domainEvent, cancellationToken);
    }

    private async Task CreditOrganizationAsync(
        CommunityEventPublishedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (domainEvent.OrganizationId is not { } organizationId)
        {
            return;
        }

        var organization = await _organizations.GetAsync(organizationId, cancellationToken);

        if (organization is null)
        {
            return;
        }

        organization.SettleStatistics(domainEvent.PublishedAt, publishedEvents: 1);
        organization.RecordActivity(domainEvent.PublishedAt);
    }

    private async Task CountCategoryAsync(
        CommunityEventPublishedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var category = await _categories.FindByKeyAsync(
            domainEvent.CategoryKey, cancellationToken);

        category?.AdjustCount(1, domainEvent.PublishedAt);
    }
}
