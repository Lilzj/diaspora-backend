using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Diaspora.Contracts.Marketplace.V1.Events;

namespace Diaspora.Modules.Financial.Application.Consumers.Subjects;

/// <summary>Ends placements over an archived marketplace listing.</summary>
public sealed class ListingArchivedConsumer : SubjectRetirementConsumer<ListingArchivedIntegrationEvent>
{
    public ListingArchivedConsumer(
        IPlacementRepository placements,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<ListingArchivedConsumer> logger)
        : base(placements, unitOfWork, clock, logger)
    {
    }

    protected override MonetizationSubjectType SubjectType => MonetizationSubjectType.MarketplaceListing;

    protected override string Reason => "The listing was archived.";

    protected override Guid? RetiredSubjectId(ListingArchivedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return integrationEvent.ListingId;
    }
}
