namespace Diaspora.Modules.Analytics;

/// <summary>
/// The module's feature gates.
/// <para>
/// Three, so a deployment can run ingestion without publishing dashboards — which is the order a
/// deployment actually needs, because an analytics dashboard turned on before any events have been
/// collected renders a correct zero and reads as a defect.
/// </para>
/// </summary>
public static class AnalyticsFeatures
{
    private const string Prefix = "analytics";

    /// <summary>Creator analytics: the overview, the trends and content performance.</summary>
    public const string Creator = Prefix + ".creator";

    /// <summary>Per-item content performance for an owner.</summary>
    public const string Content = Prefix + ".content";

    /// <summary>Client telemetry ingestion.</summary>
    public const string Telemetry = Prefix + ".telemetry";
}
