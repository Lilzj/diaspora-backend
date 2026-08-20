namespace Diaspora.Modules.GeographicIntelligence;

/// <summary>The authorisation policies this module's endpoints reference.</summary>
public static class GeographicIntelligencePolicies
{
    private const string Prefix = "geo";

    public const string IntelligenceRead = Prefix + ".intelligence.read";

    public const string RoutingRead = Prefix + ".routing.read";

    public const string CapabilitiesRead = Prefix + ".capabilities.read";
}
