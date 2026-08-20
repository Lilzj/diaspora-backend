namespace Diaspora.Platform.Modules.Contributors;

/// <summary>
/// Implemented by a module to register its telemetry sources with the host's
/// OpenTelemetry pipeline.
/// <para>
/// Naming a source is not the same as emitting from one. B2 registered three activity
/// source names and never created an <c>ActivitySource</c> behind any of them, so the
/// tracer subscribed to sources that produced nothing. A module implementing this
/// interface is expected to own real <c>ActivitySource</c> and <c>Meter</c> instances with
/// these exact names.
/// </para>
/// </summary>
public interface IObservabilityContributor
{
    /// <summary>The owning module's name.</summary>
    string Module { get; }

    /// <summary>
    /// <c>ActivitySource</c> names to subscribe the tracer to. Must match the names of
    /// live <c>ActivitySource</c> instances the module creates.
    /// </summary>
    IEnumerable<string> ActivitySources => [];

    /// <summary>
    /// <c>Meter</c> names to subscribe the metrics pipeline to. Must match the names of
    /// live <c>Meter</c> instances the module creates.
    /// </summary>
    IEnumerable<string> Meters => [];
}
