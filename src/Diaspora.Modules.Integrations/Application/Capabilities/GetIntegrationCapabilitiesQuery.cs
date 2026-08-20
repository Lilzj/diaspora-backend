using Diaspora.Application.Abstractions;
using Diaspora.Modules.Integrations.Contracts.V1.Responses;
using Diaspora.Modules.Integrations.Extensibility.Providers;
using Diaspora.Modules.Integrations.Infrastructure.Providers;
using Diaspora.Modules.Integrations.Options;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Integrations.Application.Capabilities;

/// <summary>
/// What this deployment actually has.
/// <para>
/// <b>The route that makes "NotConfigured" a state a client can read rather than infer from a
/// refusal.</b> B21 established the pattern; this applies it to the integration boundary, where the
/// answer changes per deployment more than anywhere else on the platform — a deployment with a
/// Google adapter and no allow-listed redirect can start no authorization, and without this route
/// that is only discoverable by trying.
/// </para>
/// <para>
/// <b>Every false comes with a reason.</b> A capability report that said only "no" would leave an
/// operator guessing between "no adapter", "no credential", "no redirect" and "no key" — four
/// different fixes.
/// </para>
/// </summary>
public sealed record GetIntegrationCapabilitiesQuery : IQuery<IntegrationCapabilitiesResponse>;

public sealed class GetIntegrationCapabilitiesQueryHandler
    : IQueryHandler<GetIntegrationCapabilitiesQuery, IntegrationCapabilitiesResponse>
{
    private readonly IntegrationProviderRegistry _registry;
    private readonly IIntegrationCredentialProtector _protector;
    private readonly IOptionsMonitor<IntegrationsOptions> _options;

    public GetIntegrationCapabilitiesQueryHandler(
        IntegrationProviderRegistry registry,
        IIntegrationCredentialProtector protector,
        IOptionsMonitor<IntegrationsOptions> options)
    {
        _registry = registry;
        _protector = protector;
        _options = options;
    }

    public Task<Result<IntegrationCapabilitiesResponse>> Handle(
        GetIntegrationCapabilitiesQuery query, CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        var reasons = new Dictionary<string, string>(StringComparer.Ordinal);

        var oauth = _registry.ConfiguredOAuthProviders;
        var calendar = _registry.ConfiguredCalendarProviders;

        var redirectsConfigured = options.AllowedRedirectUris.Any(
            entry => !string.IsNullOrWhiteSpace(entry));

        if (oauth.Count == 0)
        {
            reasons["oauth"] =
                "This deployment has no OAuth adapter registered. Connecting an external account "
                + "needs one.";
        }

        if (calendar.Count == 0)
        {
            reasons["calendar"] =
                "This deployment has no calendar adapter registered. Calendars cannot be listed or "
                + "written to.";
        }

        if (!redirectsConfigured)
        {
            reasons["oauth.redirect"] =
                "No redirect address is allow-listed, so no authorization can be started. This is "
                + "the correct default: accepting a client-supplied redirect is the canonical OAuth "
                + "code-interception vulnerability.";
        }

        if (!_protector.IsConfigured)
        {
            reasons["credentialCustody"] =
                "No credential protection key is available, so a provider token cannot be stored "
                + "safely and no authorization will be completed.";
        }

        if (options.AllowedOutboundHosts.All(string.IsNullOrWhiteSpace))
        {
            reasons["outbound"] =
                "No outbound host is allow-listed, so no provider call will leave this deployment.";
        }

        var capabilities = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var twoWay = false;

        foreach (var key in calendar)
        {
            var declared = _registry.CalendarCapabilities(key);

            capabilities[key] =
                [.. declared.Select(capability => capability.ToString()).Order(StringComparer.Ordinal)];

            if (declared.Contains(ExternalCalendarCapability.ImportEvents)
                && declared.Contains(ExternalCalendarCapability.WatchChannel))
            {
                twoWay = true;
            }
        }

        if (!twoWay)
        {
            // Stated rather than left to be inferred. The register: until the watch channel exists
            // "the client must never render 'synced'", and this is the server saying so in a field.
            reasons["calendar.sync.twoWay"] =
                "Two-way calendar synchronisation needs an adapter that can both import entries and "
                + "register a push channel. No adapter on this deployment declares both, so nothing "
                + "may be described as synced in both directions.";
        }

        return Task.FromResult(Result.Success(new IntegrationCapabilitiesResponse
        {
            OAuthProviders = oauth,
            CalendarProviders = calendar,
            CalendarCapabilities = capabilities,
            WebhookProviders = _registry.WebhookProviders,
            CredentialCustodyConfigured = _protector.IsConfigured,
            RedirectUrisConfigured = redirectsConfigured,
            TwoWayCalendarSync = twoWay,
            UnavailableReasons = reasons
        }));
    }
}
