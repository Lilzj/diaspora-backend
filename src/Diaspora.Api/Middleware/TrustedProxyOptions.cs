using System.ComponentModel.DataAnnotations;

namespace Diaspora.Api.Middleware;

/// <summary>
/// Which upstream addresses may set <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c> for this
/// deployment.
/// <para>
/// <b>This is a trust decision, and it is a declared type rather than a configuration lookup
/// because the lookup form is what hid a vulnerability for a phase.</b> The host used to clear
/// both of <see cref="Microsoft.AspNetCore.Builder.ForwardedHeadersOptions"/>'s address
/// collections and repopulate only <c>KnownProxies</c> from configuration, under a comment saying
/// that an empty allow-list meant the headers were ignored. The opposite is true:
/// <c>ForwardedHeadersMiddleware</c> computes
/// <c>checkKnownIps = KnownNetworks.Count &gt; 0 || KnownProxies.Count &gt; 0</c> and skips the
/// known-address check altogether when both are empty, applying whatever the caller sent. A forged
/// <c>X-Forwarded-For</c> therefore became <c>Connection.RemoteIpAddress</c>, which is the
/// rate-limit partition key and the address written to every <c>SecurityEvent</c> and
/// <c>UserSession</c>.
/// </para>
/// <para>
/// <b>Naming the section as a type buys three things the lookups could not.</b>
/// <see cref="KnownIPNetworks"/> is bindable at all — it was cleared in code and had no
/// configuration path back, so the only setting a Fly.io deployment can actually use could not be
/// expressed. An unparseable entry is refused at startup instead of being silently dropped, which
/// is what the old <c>TryParse</c>-without-an-else did. And an empty list reaches
/// <see cref="TrustedProxyOptionsValidator"/>, which refuses to start a deployed host — so the
/// flat-environment-variable mistake below fails loudly rather than silently.
/// </para>
/// <para>
/// <b>Both lists are arrays, so an environment variable must be indexed.</b>
/// <c>ForwardedHeaders__KnownIPNetworks=172.16.0.0/12</c> binds to <i>nothing</i>: configuration
/// reads an array from indexed child keys, so a section holding a scalar has a value and no
/// children, and the bound list stays empty. The form that binds is
/// <c>ForwardedHeaders__KnownIPNetworks__0</c>. Measured on .NET 10.0.13, not inferred. This is
/// the single most likely way for a correct intention to leave the platform exposed, which is why
/// the empty case is fatal outside Development.
/// </para>
/// </summary>
public sealed class TrustedProxyOptions
{
    public const string SectionName = "ForwardedHeaders";

    /// <summary>
    /// How many forwarded entries the middleware walks, counting from the right. One, because the
    /// deployment has exactly one trusted hop: anything further left in the header was supplied by
    /// something this platform does not control.
    /// </summary>
    [Range(1, 16)]
    public int Limit { get; set; } = 1;

    /// <summary>
    /// Individual ingress addresses to trust. Usable when an ingress has a stable address; on
    /// Fly.io it does not, because the proxy connects from the host side of the machine's private
    /// interface and that address moves with the machine. Prefer
    /// <see cref="KnownIPNetworks"/> there.
    /// </summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>
    /// Ingress networks to trust, in CIDR form. The entry that matters for this deployment:
    /// Fly's proxy was observed reaching the app from <c>172.19.11.233</c> — the default gateway
    /// on the machine's <c>eth0</c>, which sits on <c>172.19.11.232/29</c> — as an IPv4-mapped
    /// IPv6 peer, which <c>ForwardedHeadersMiddleware.CheckKnownAddress</c> unwraps before
    /// testing. Note that this is <i>not</i> a 6PN <c>fdaa::</c> address: 6PN carries
    /// machine-to-machine traffic and SSH, and bypasses the proxy entirely.
    /// </summary>
    public string[] KnownIPNetworks { get; set; } = [];
}
