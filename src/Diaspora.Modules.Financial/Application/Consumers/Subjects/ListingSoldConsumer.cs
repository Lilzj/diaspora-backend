using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Diaspora.Contracts.Marketplace.V1.Events;

namespace Diaspora.Modules.Financial.Application.Consumers.Subjects;

/// <summary>Ends placements over a marketplace listing once it is sold out.
/// <para>
/// <b>Only when it is sold <em>out</em>.</b> A listing with three of five sold is still worth
/// promoting, and ending its boost on the first sale would take away what the seller paid for at
/// precisely the moment it was working.
/// </para></summary>
public sealed class ListingSoldConsumer : SubjectRetirementConsumer<ListingSoldIntegrationEvent>
{
    public ListingSoldConsumer(
        IPlacementRepository placements,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<ListingSoldConsumer> logger)
        : base(placements, unitOfWork, clock, logger)
    {
    }

    protected override MonetizationSubjectType SubjectType => MonetizationSubjectType.MarketplaceListing;

    protected override string Reason => "The listing sold out.";

    protected override Guid? RetiredSubjectId(ListingSoldIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return integrationEvent.IsSoldOut ? integrationEvent.ListingId : null;
    }
}
