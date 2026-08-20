namespace Diaspora.Modules.GeographicIntelligence.Contracts.V1.Responses;

/// <summary>One geographic capability and whether this deployment actually has it.</summary>
public sealed record GeoCapabilityResponse
{
    /// <summary><c>routing</c>, <c>transit-routing</c>, <c>heat-layers</c>, <c>neighborhood-scores</c>, …</summary>
    public required string Capability { get; init; }

    /// <summary>Whether a caller will get real data. False is a normal, publishable answer.</summary>
    public required bool Available { get; init; }

    /// <summary>The provider key behind it — <c>none</c>, <c>catalog</c>, <c>platform</c>. Never a credential.</summary>
    public required string Provider { get; init; }

    /// <summary>What is actually behind the capability, or what is missing. One sentence.</summary>
    public required string Detail { get; init; }
}

/// <summary>
/// What this deployment's geographic stack can do.
/// <para>
/// <b>A client should read this before it renders a Directions button, not after it fails.</b>
/// Phase 8's provider register describes a UI that must "distinguish 'no service' from 'we do not
/// know'"; that distinction cannot be made from a failed request, only from a declaration. This is
/// the declaration, and it is authorised one tier below everything it describes so that a client
/// can always ask.
/// </para>
/// <para>
/// It reports what is <em>configured</em>, never how. No endpoint, no key, no vendor account and
/// no quota appears here — only a stable provider key the deployment chose.
/// </para>
/// </summary>
public sealed record GeoCapabilitiesResponse
{
    public required IReadOnlyList<GeoCapabilityResponse> Capabilities { get; init; }

    /// <summary>Travel modes a configured provider will actually answer. Empty when none is.</summary>
    public required IReadOnlyList<string> SupportedTravelModes { get; init; }

    /// <summary>Density layers this platform can compute from real counts.</summary>
    public required IReadOnlyList<string> SupportedHeatLayers { get; init; }

    /// <summary>
    /// Score dimensions that could be served for some place on this deployment.
    /// <para>
    /// Which of them a <em>particular</em> neighbourhood has is still per-neighbourhood — a place
    /// nobody has assessed has none of them — but a client can use this to decide whether the
    /// breakdown surface is worth building a tab for at all.
    /// </para>
    /// </summary>
    public required IReadOnlyList<string> SupportedScoreDimensions { get; init; }
}
