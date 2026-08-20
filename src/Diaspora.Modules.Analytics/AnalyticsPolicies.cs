namespace Diaspora.Modules.Analytics;

/// <summary>
/// The policy names this module's endpoints reference.
/// <para>
/// Lower case, dotted, mirroring the permission each fronts. Policy names are backend-only — no
/// token carries one and no client holds one — which is why renaming one is cheap and renaming a
/// permission key is not.
/// </para>
/// </summary>
public static class AnalyticsPolicies
{
    private const string Prefix = "analytics";

    public const string CreatorRead = Prefix + ".creator.read";

    public const string ContentRead = Prefix + ".content.read";

    public const string TelemetryWrite = Prefix + ".telemetry.write";

    public const string PlatformRead = Prefix + ".platform.read";

    public const string OperationsRun = Prefix + ".operations.run";
}
