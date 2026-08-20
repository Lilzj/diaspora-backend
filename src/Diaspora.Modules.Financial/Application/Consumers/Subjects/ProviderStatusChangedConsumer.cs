using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Diaspora.Contracts.Providers.V1.Events;

namespace Diaspora.Modules.Financial.Application.Consumers.Subjects;

/// <summary>Ends placements over an archived provider.
/// <para>
/// <b>Archived only.</b> A provider on holiday or temporarily closed is still a provider somebody
/// paid to feature, and withdrawing their placement for a fortnight away would be taking back what
/// they bought.
/// </para></summary>
public sealed class ProviderStatusChangedConsumer : SubjectRetirementConsumer<ProviderStatusChangedIntegrationEvent>
{
    public ProviderStatusChangedConsumer(
        IPlacementRepository placements,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<ProviderStatusChangedConsumer> logger)
        : base(placements, unitOfWork, clock, logger)
    {
    }

    protected override MonetizationSubjectType SubjectType => MonetizationSubjectType.Provider;

    protected override string Reason => "The provider was archived.";

    protected override Guid? RetiredSubjectId(ProviderStatusChangedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return string.Equals(integrationEvent.CurrentStatus, "Archived", StringComparison.OrdinalIgnoreCase) ? integrationEvent.ProviderId : null;
    }
}
