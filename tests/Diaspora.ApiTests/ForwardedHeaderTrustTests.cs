using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Diaspora.ApiTests;

/// <summary>
/// Pins what <c>UseForwardedHeaders</c> actually does, against a pipeline carrying only that
/// middleware.
/// <para>
/// Deliberately not the Diaspora host. These assertions are about a framework contract the
/// platform's security depends on, and a test that booted every module would prove the same thing
/// far more slowly while sharing a model cache and a seeder set with the integration suite. What
/// the host does with these options is covered by
/// <see cref="TrustedProxyOptionsValidatorTests"/>.
/// </para>
/// <para>
/// The first test is the regression test for BSEC-001 itself. It asserts the *vulnerable*
/// behaviour, on purpose: two comments in this repository claimed an empty allow-list meant the
/// headers were ignored, and nothing executed the claim for a phase. Pinning the real behaviour is
/// what stops the configuration being emptied again by someone who believes the comment.
/// </para>
/// </summary>
public class ForwardedHeaderTrustTests
{
    /// <summary>
    /// The address Fly's proxy was observed connecting from, in the shape the app sees it.
    /// Measured by reading <c>/proc/net/tcp6</c> inside machine <c>867615be4673d8</c> while
    /// traffic flowed: connections to <c>:8080</c> arrive from <c>172.19.11.233</c>, IPv4-mapped,
    /// because Kestrel binds dual-stack.
    /// </summary>
    private const string FlyProxyPeer = "::ffff:172.19.11.233";

    private const string TrustedNetwork = "172.16.0.0/12";
    private const string SixPnNetwork = "fdaa::/16";
    private const string UntrustedPeer = "198.51.100.9";
    private const string ForgedClient = "203.0.113.77";

    [Fact]
    public async Task An_empty_allow_list_applies_a_forged_header_from_any_peer()
    {
        // ForwardedHeadersMiddleware computes
        //   checkKnownIps = KnownNetworks.Count > 0 || KnownProxies.Count > 0
        // and skips the known-address check entirely when both are empty. Empty therefore means
        // "trust everyone", not "trust no one" — the opposite of what this platform's comments
        // said, and the reason a deployed host now refuses to start in that state.
        var observed = await ObservedClientAddress(
            trustedNetwork: null, peer: UntrustedPeer, forwardedFor: ForgedClient);

        observed.Should().Be(IPAddress.Parse(ForgedClient),
            "this is the defect BSEC-001 recorded, pinned so it cannot return quietly");
    }

    [Fact]
    public async Task The_measured_fly_peer_is_trusted_through_the_configured_network()
    {
        // The IPv4-mapped-IPv6 unwrapping this depends on lives in
        // ForwardedHeadersMiddleware.CheckKnownAddress, which maps to IPv4 and re-checks before
        // testing the networks. An IPv4 /12 therefore does match an IPv4-mapped IPv6 peer — which
        // was reasoned from the framework's source first and is asserted here instead.
        var observed = await ObservedClientAddress(
            trustedNetwork: TrustedNetwork, peer: FlyProxyPeer, forwardedFor: ForgedClient);

        observed.Should().Be(IPAddress.Parse(ForgedClient),
            "the real client address is what the limiter and the audit trail should record");
    }

    [Fact]
    public async Task A_peer_outside_the_configured_network_cannot_set_its_own_address()
    {
        var observed = await ObservedClientAddress(
            trustedNetwork: TrustedNetwork, peer: UntrustedPeer, forwardedFor: ForgedClient);

        observed.Should().Be(IPAddress.Parse(UntrustedPeer),
            "an untrusted caller's header is ignored and its real peer address stands");
    }

    [Fact]
    public async Task A_6pn_allow_list_would_not_match_the_proxy_and_would_fail_silently()
    {
        // fdaa::/16 is the obvious guess for Fly, and the audit's own first remediation note made
        // it. It matches nothing: 6PN carries machine-to-machine traffic and SSH and bypasses the
        // proxy. The failure is quiet in the dangerous direction — not an error, just every
        // member collapsed into one rate-limit bucket, which is the other half of what
        // RateLimitingRegistration.PartitionKey warns about.
        var observed = await ObservedClientAddress(
            trustedNetwork: SixPnNetwork, peer: FlyProxyPeer, forwardedFor: ForgedClient);

        observed.Should().Be(IPAddress.Parse(FlyProxyPeer),
            "a wrong network is indistinguishable from no forwarded headers at all");
    }

    [Fact]
    public async Task An_untrusted_peer_cannot_assert_https_over_plaintext()
    {
        var scheme = await ObservedScheme(TrustedNetwork, UntrustedPeer);

        scheme.Should().Be("http",
            "Request.IsHttps feeds generated media URLs through MediaUrlFactory's origin fallback");
    }

    [Fact]
    public async Task The_trusted_proxy_may_report_the_original_scheme()
    {
        var scheme = await ObservedScheme(TrustedNetwork, FlyProxyPeer);

        scheme.Should().Be("https", "Fly terminates TLS and reports it with X-Forwarded-Proto");
    }

    // ─── Harness ────────────────────────────────────────────────────────────────────────────

    private static async Task<IPAddress?> ObservedClientAddress(
        string? trustedNetwork,
        string peer,
        string forwardedFor)
    {
        var context = await Send(trustedNetwork, peer, request =>
            request.Headers["X-Forwarded-For"] = forwardedFor);

        return context.Connection.RemoteIpAddress;
    }

    private static async Task<string> ObservedScheme(string trustedNetwork, string peer)
    {
        var context = await Send(trustedNetwork, peer, request =>
            request.Headers["X-Forwarded-Proto"] = "https");

        return context.Request.Scheme;
    }

    private static async Task<HttpContext> Send(
        string? trustedNetwork,
        string peer,
        Action<HttpRequest> configureRequest)
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services => services.Configure<ForwardedHeadersOptions>(options =>
                {
                    // The host's own projection, in TrustedProxyOptions terms.
                    options.ForwardedHeaders =
                        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                    options.ForwardLimit = 1;
                    options.KnownProxies.Clear();
                    options.KnownIPNetworks.Clear();

                    if (trustedNetwork is not null)
                    {
                        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(trustedNetwork));
                    }
                }))
                .Configure(app =>
                {
                    app.UseForwardedHeaders();
                    app.Run(_ => Task.CompletedTask);
                }))
            .StartAsync();

        return await host.GetTestServer().SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
            context.Request.Method = "GET";
            context.Request.Scheme = "http";
            context.Request.Host = new HostString("diaspora-api.fly.dev");
            context.Request.Path = "/";

            configureRequest(context.Request);
        });
    }
}
