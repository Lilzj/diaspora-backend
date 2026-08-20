namespace Diaspora.Modules.GeographicIntelligence;

/// <summary>
/// The module's feature gates.
/// <para>
/// Split so a deployment can serve neighbourhood intelligence without offering routing. Until a
/// routing vendor is contracted, <see cref="Routing"/> off and the capability report saying so is
/// the honest configuration — the endpoint answers <c>NotConfigured</c> either way, but a client
/// that reads the gate never renders the button.
/// </para>
/// </summary>
public static class GeographicIntelligenceFeatures
{
    private const string Prefix = "geo";

    public const string Intelligence = Prefix + ".intelligence";

    public const string Routing = Prefix + ".routing";
}
