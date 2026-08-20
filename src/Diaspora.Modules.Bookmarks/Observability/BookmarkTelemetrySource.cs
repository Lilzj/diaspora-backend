using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Diaspora.Modules.Bookmarks.Observability;

/// <summary>
/// The module's live telemetry instruments.
/// <para>
/// A module that declares an activity source through <c>IObservabilityContributor</c> must
/// also create one, or it subscribes the tracer to silence — exactly the B2 mistake this
/// whole strand of B2.6 exists to prevent. These are real; the endpoints below emit through
/// them.
/// </para>
/// </summary>
public static class BookmarkTelemetrySource
{
    public static readonly ActivitySource Activity = new(BookmarkTelemetry.SourceName, "1.0.0");

    public static readonly Meter Meter = new(BookmarkTelemetry.SourceName, "1.0.0");

    public static readonly Counter<long> Created =
        Meter.CreateCounter<long>(
            "diaspora.bookmarks.created",
            unit: "{bookmark}",
            description: "Bookmarks created.");

    public static readonly Counter<long> Removed =
        Meter.CreateCounter<long>(
            "diaspora.bookmarks.removed",
            unit: "{bookmark}",
            description: "Bookmarks removed.");
}
