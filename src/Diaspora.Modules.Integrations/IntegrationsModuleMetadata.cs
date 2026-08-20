namespace Diaspora.Modules.Integrations;

/// <summary>Names shared by the module's platform module and its contributors.</summary>
public static class IntegrationsModuleMetadata
{
    public const string Name = "Integrations";

    public const string Version = "1.0.0";

    /// <summary>The module's own database schema. It owns these tables and no others.</summary>
    public const string Schema = "integrations";

    /// <summary>
    /// The module's public URL space.
    /// <para>
    /// One prefix, and it is the frontend's. The client's <c>integrations-gap-api.ts</c> declares
    /// every route in this module against <c>/integrations-gap/v0</c> with the note that "when a
    /// backend phase ships integrations, the hook swaps which arm it calls and the request shapes
    /// already match". They match. Only the prefix changes.
    /// </para>
    /// </summary>
    public const string RoutePrefix = "/api/v1/integrations";

    /// <summary>
    /// The dependency name every outbound provider call is executed under.
    /// <para>
    /// Suffixed per provider — <c>integrations.calendar.google</c> — so a breaker opened by one
    /// vendor does not refuse calls to another. B17's executor resolves the retry policy, the
    /// breaker and the bulkhead from this string and labels every metric with it.
    /// </para>
    /// </summary>
    public const string ResilienceDependencyPrefix = "integrations";
}
