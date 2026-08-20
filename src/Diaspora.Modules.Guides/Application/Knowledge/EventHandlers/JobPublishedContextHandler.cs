using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Domain.Enums;

namespace Diaspora.Modules.Guides.Application.Knowledge.EventHandlers;

/// <summary>
/// Counts a job towards its country's dashboard band.
/// <para>
/// <b>Consumes B9's published contract, not B9.</b> The Knowledge Directory has no reference to the
/// Jobs assembly and must not: Jobs consumes this module, and a reference the other way would invert
/// the dependency direction the architecture tests enforce.
/// </para>
/// <para>
/// Guarded on first publication, so a republished job does not count twice.
/// </para>
/// </summary>
public sealed class JobPublishedContextHandler
    : IntegrationEventHandler<JobPublishedIntegrationEvent>
{
    private readonly CountryContextProjector _projector;

    public JobPublishedContextHandler(CountryContextProjector projector) => _projector = projector;

    protected override Task HandleAsync(
        JobPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return integrationEvent.IsFirstPublication
            ? _projector.ApplyAsync(
                integrationEvent.CountryCode, CountryContextKind.Jobs, 1, cancellationToken)
            : Task.CompletedTask;
    }
}
