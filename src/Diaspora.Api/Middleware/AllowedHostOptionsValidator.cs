using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Diaspora.Api.Middleware;

/// <summary>
/// Refuses to start a deployed host that accepts any Host header, and refuses, anywhere, an entry
/// that could never match one (BSEC-012).
/// <para>
/// <c>AllowedHosts: "*"</c> turns host filtering off. The practical risk on this platform is a URL
/// built from the request's own host: <c>MediaUrlFactory</c> falls back to it when no public base URL
/// is configured, so a forged Host header could steer a link the API hands out. A deployment names
/// the hosts it serves.
/// </para>
/// <para>
/// A malformed entry fails the other way, so it is refused in every environment. A scheme, a path or
/// a port makes an entry match nothing, and a list whose entries match nothing refuses every request
/// the deployment exists to serve.
/// </para>
/// <para>
/// Development and Testing may accept any host, for the reasons
/// <see cref="TrustedProxyOptionsValidator"/> gives: a developer host has no public name, the
/// integration suite runs as Testing, and the build's own OpenAPI generation starts the application
/// as Development.
/// </para>
/// </summary>
public sealed class AllowedHostOptionsValidator : IValidateOptions<AllowedHostOptions>
{
    private readonly IHostEnvironment _environment;

    public AllowedHostOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, AllowedHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        foreach (var host in options.Hosts)
        {
            if (host != "*" && !IsHostName(host))
            {
                failures.Add(
                    $"'{AllowedHostOptions.ConfigurationKey}' contains '{host}', which is not a host " +
                    "name. Entries take no scheme, path or port: 'diaspora-api.fly.dev', not " +
                    "'https://diaspora-api.fly.dev' or 'diaspora-api.fly.dev:443'. An entry like that " +
                    "matches no request, so a list of them refuses every one.");
            }
        }

        if (options.AcceptsAnyHost && !IsExempt())
        {
            failures.Add(
                $"'{AllowedHostOptions.ConfigurationKey}' accepts any host in the " +
                $"'{_environment.EnvironmentName}' environment: it is " +
                (options.Hosts.Count == 0 ? "not set" : "'*'") + ". A forged Host header then " +
                "reaches every handler, and a URL built from the request's own host follows it. Name " +
                "the hosts this deployment answers on, separated by ';'. On Fly.io that is " +
                "AllowedHosts = 'diaspora-api.fly.dev' in fly.toml's [env], plus any custom domain " +
                "attached with 'fly certs add'. The orchestrator's probes need no entry: they are " +
                "answered whatever Host they send.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>
    /// A name, a <c>*.</c> wildcard over one, or a bracketed IPv6 literal: the forms ASP.NET Core
    /// matches. Nothing with a scheme, a path or a port.
    /// </summary>
    private static bool IsHostName(string host)
    {
        if (host.StartsWith('[') && host.EndsWith(']'))
        {
            return host.Length > 2;
        }

        var name = host.StartsWith("*.", StringComparison.Ordinal) ? host[2..] : host;

        // Uri.CheckHostName refuses a scheme, a path, a port, a space or a stray '*', and the empty
        // string. It accepts a bare IPv6 address, which a Host header never carries unbracketed, so
        // that case is refused here.
        return !name.Contains(':')
            && Uri.CheckHostName(name) != UriHostNameType.Unknown;
    }

    private bool IsExempt() =>
        _environment.IsDevelopment() || _environment.IsEnvironment("Testing");
}
