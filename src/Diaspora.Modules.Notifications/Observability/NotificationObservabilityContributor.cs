using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Notifications.Observability;

/// <summary>
/// Subscribes the host's tracer and metrics to the module's telemetry source.
/// <para>
/// The <c>ActivitySource</c> and <c>Meter</c> behind these names are created in
/// <see cref="NotificationTelemetry"/>. Declaring a source without creating one subscribes the
/// tracer to silence, so both are always done together.
/// </para>
/// </summary>
public sealed class NotificationObservabilityContributor : IObservabilityContributor
{
    public string Module => NotificationsModuleMetadata.Name;

    public IEnumerable<string> ActivitySources => [NotificationTelemetry.SourceName];

    public IEnumerable<string> Meters => [NotificationTelemetry.SourceName];
}
