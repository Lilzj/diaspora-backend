using Diaspora.Contracts.Providers.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Providers;

/// <summary>
/// A provider's status moved.
/// <para>
/// <b>Providers publishes status transitions rather than a withdrawal contract, so this consumer has
/// to interpret the destination.</b> Suspended, closed and archived all mean "stop recommending
/// this"; published means the opposite. The list of stopping states is explicit rather than
/// exclusionary — a status this module does not recognise leaves the projection alone, because
/// guessing that an unfamiliar state means "hide" would silently empty the surface the first time
/// Providers adds one.
/// </para>
/// </summary>
public sealed class ProviderStatusChangedConsumer
    : RecommendationEventConsumer<ProviderStatusChangedIntegrationEvent>
{
    /// <summary>States that stop a provider being recommended. Explicit — see the class summary.</summary>
    private static readonly string[] WithdrawnStates =
        ["suspended", "closed", "archived", "removed", "unpublished", "draft"];

    public ProviderStatusChangedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Providers;

    protected override async Task HandleAsync(
        ProviderStatusChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var status = integrationEvent.CurrentStatus.Trim().ToLowerInvariant();

        if (WithdrawnStates.Contains(status))
        {
            await Projector.WithdrawAsync(
                RecommendationTargetType.Provider, integrationEvent.ProviderId, cancellationToken);
        }
    }
}
