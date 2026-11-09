using System.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Diaspora.Api.Middleware;

/// <summary>
/// Refuses to start a deployed host that trusts forwarded headers from anywhere, and refuses an
/// address or network it cannot parse.
/// <para>
/// The empty case is the one that matters. ASP.NET Core applies <c>X-Forwarded-For</c>
/// unconditionally when neither address collection is populated, so an unconfigured ingress is not
/// a host that ignores the header — it is a host where any caller sets its own client address.
/// That makes every per-IP rate limit forgeable per request and makes the security audit trail
/// attacker-controlled. The platform already refuses to start on a missing signing key
/// (<c>JwtOptionsValidator</c>) and on rate limiting switched off in a deployed environment
/// (<c>RateLimitingOptionsValidator</c>); an unconfigured proxy allow-list belongs in the same
/// category, because it is a misconfiguration rather than a default.
/// </para>
/// <para>
/// Development and Testing are exempt, and that exemption is load-bearing rather than
/// convenience. A developer host has no ingress in front of it, the integration suite runs as
/// <c>Testing</c> through <c>CustomWebApplicationFactory</c>, <c>WebApplicationFactory</c> boots
/// the API suite as Development, and the build's own OpenAPI document generation starts the whole
/// application as Development to emit <c>contracts/Diaspora.Api.json</c>. Refusing in those three
/// would fail the test suites and the build rather than protect anything — the same exemption
/// shape, and for the same reason, as <c>RateLimitingOptionsValidator</c>.
/// </para>
/// </summary>
public sealed class TrustedProxyOptionsValidator : IValidateOptions<TrustedProxyOptions>
{
    private readonly IHostEnvironment _environment;

    public TrustedProxyOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, TrustedProxyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        foreach (var proxy in options.KnownProxies)
        {
            if (!IPAddress.TryParse(proxy, out _))
            {
                failures.Add(
                    $"'{TrustedProxyOptions.SectionName}:KnownProxies' contains '{proxy}', which " +
                    "is not an IP address. A value that cannot be parsed used to be dropped in " +
                    "silence, which left the allow-list emptier than the configuration claimed.");
            }
        }

        foreach (var network in options.KnownIPNetworks)
        {
            if (!IPNetwork.TryParse(network, out _))
            {
                failures.Add(
                    $"'{TrustedProxyOptions.SectionName}:KnownIPNetworks' contains '{network}', " +
                    "which is not a CIDR network. Expected a form like '172.16.0.0/12'.");
            }
        }

        if (options.KnownProxies.Length == 0 && options.KnownIPNetworks.Length == 0 && !IsExempt())
        {
            failures.Add(
                $"'{TrustedProxyOptions.SectionName}:KnownProxies' and " +
                $"'{TrustedProxyOptions.SectionName}:KnownIPNetworks' are both empty in the " +
                $"'{_environment.EnvironmentName}' environment. ASP.NET Core applies " +
                "'X-Forwarded-For' unconditionally when neither is set, so every per-IP rate " +
                "limit becomes forgeable and every recorded client address becomes " +
                "attacker-controlled. Set the ingress network this host sits behind. On Fly.io " +
                "the proxy reaches the app over the machine's private IPv4 interface, so: " +
                $"{TrustedProxyOptions.SectionName}__KnownIPNetworks__0=172.16.0.0/12 — note the " +
                "'__0'. A flat " +
                $"'{TrustedProxyOptions.SectionName}__KnownIPNetworks' binds to nothing and would " +
                "leave this list empty.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private bool IsExempt() =>
        _environment.IsDevelopment() || _environment.IsEnvironment("Testing");
}
