using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Diaspora.Contracts.Housing.V1.Events;

namespace Diaspora.Modules.Financial.Application.Consumers.Subjects;

/// <summary>Ends placements over a fully let property.
/// <para>
/// A partially occupied property still has rooms to fill, so its highlight keeps running.
/// </para></summary>
public sealed class HousingOccupiedConsumer : SubjectRetirementConsumer<HousingOccupiedIntegrationEvent>
{
    public HousingOccupiedConsumer(
        IPlacementRepository placements,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<HousingOccupiedConsumer> logger)
        : base(placements, unitOfWork, clock, logger)
    {
    }

    protected override MonetizationSubjectType SubjectType => MonetizationSubjectType.HousingListing;

    protected override string Reason => "The property was let.";

    protected override Guid? RetiredSubjectId(HousingOccupiedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return integrationEvent.IsFullyOccupied ? integrationEvent.ListingId : null;
    }
}
