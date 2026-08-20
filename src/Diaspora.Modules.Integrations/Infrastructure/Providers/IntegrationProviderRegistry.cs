using Diaspora.Modules.Integrations.Extensibility.Providers;

namespace Diaspora.Modules.Integrations.Infrastructure.Providers;

/// <summary>
/// Which adapters this deployment actually has.
/// <para>
/// <b>Resolution is by exact provider key and there is no fallback.</b> A request for
/// <c>calendar.google</c> that finds no adapter gets the not-configured answer — it does not fall
/// back to <c>calendar.apple</c>, and it does not fall back to "the only calendar adapter
/// registered". Either would mean a member's Google authorization silently wrote into a different
/// vendor's calendar.
/// </para>
/// <para>
/// <b>A registered adapter that reports <c>IsConfigured == false</c> is treated as absent.</b> An
/// adapter assembly that ships without its credentials is the commonest deployment mistake on this
/// surface, and the honest reading is "this deployment cannot do it" rather than "this deployment
/// can do it and will fail every time".
/// </para>
/// </summary>
public sealed class IntegrationProviderRegistry
{
    private readonly Dictionary<string, IOAuthProvider> _oauth;
    private readonly Dictionary<string, IExternalCalendarProvider> _calendar;
    private readonly Dictionary<string, IIntegrationWebhookVerifier> _verifiers;

    public IntegrationProviderRegistry(
        IEnumerable<IOAuthProvider> oauthProviders,
        IEnumerable<IExternalCalendarProvider> calendarProviders,
        IEnumerable<IIntegrationWebhookVerifier> webhookVerifiers)
    {
        ArgumentNullException.ThrowIfNull(oauthProviders);
        ArgumentNullException.ThrowIfNull(calendarProviders);
        ArgumentNullException.ThrowIfNull(webhookVerifiers);

        // Last registration wins, matching every other substitution seam on this platform: an
        // adapter registered after the module replaces whatever was there. The not-configured
        // defaults key on "none" and so never collide with a real one.
        _oauth = Index(oauthProviders, provider => provider.Key);
        _calendar = Index(calendarProviders, provider => provider.Key);
        _verifiers = Index(webhookVerifiers, verifier => verifier.Provider);
    }

    /// <summary>The OAuth adapter for a key, or null when this deployment has none configured.</summary>
    public IOAuthProvider? FindOAuth(string providerKey) =>
        Find(_oauth, providerKey, provider => provider.IsConfigured);

    /// <summary>The calendar adapter for a key, or null.</summary>
    public IExternalCalendarProvider? FindCalendar(string providerKey) =>
        Find(_calendar, providerKey, provider => provider.IsConfigured);

    /// <summary>
    /// The webhook verifier for a provider, or null.
    /// <para>
    /// <see cref="IIntegrationWebhookVerifier.IsConfiguredAsync"/> is asynchronous — it reads a
    /// secret — so it is checked by the caller rather than here. A verifier that is registered and
    /// unconfigured must still be found, because the route's answer differs: an unknown provider is
    /// a 404 and a misconfigured one is a refusal an operator needs to see.
    /// </para>
    /// </summary>
    public IIntegrationWebhookVerifier? FindVerifier(string provider) =>
        _verifiers.GetValueOrDefault(provider.Trim().ToLowerInvariant());

    /// <summary>Every configured OAuth provider key. For the capability report.</summary>
    public IReadOnlyList<string> ConfiguredOAuthProviders =>
        [.. _oauth.Values.Where(provider => provider.IsConfigured).Select(provider => provider.Key).Order(StringComparer.Ordinal)];

    /// <summary>Every configured calendar provider key. For the capability report.</summary>
    public IReadOnlyList<string> ConfiguredCalendarProviders =>
        [.. _calendar.Values.Where(provider => provider.IsConfigured).Select(provider => provider.Key).Order(StringComparer.Ordinal)];

    /// <summary>Every provider that can verify a callback. For the capability report.</summary>
    public IReadOnlyList<string> WebhookProviders =>
        [.. _verifiers.Keys.Order(StringComparer.Ordinal)];

    /// <summary>What one calendar adapter can do, or empty when there is none.</summary>
    public IReadOnlyCollection<ExternalCalendarCapability> CalendarCapabilities(string providerKey) =>
        FindCalendar(providerKey)?.Capabilities ?? [];

    private static TProvider? Find<TProvider>(
        Dictionary<string, TProvider> index, string key, Func<TProvider, bool> isConfigured)
        where TProvider : class =>
        index.GetValueOrDefault(key.Trim().ToLowerInvariant()) is { } provider && isConfigured(provider)
            ? provider
            : null;

    private static Dictionary<string, TProvider> Index<TProvider>(
        IEnumerable<TProvider> providers, Func<TProvider, string> key)
    {
        var index = new Dictionary<string, TProvider>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in providers)
        {
            var name = key(provider)?.Trim().ToLowerInvariant();

            if (!string.IsNullOrEmpty(name))
            {
                index[name] = provider;
            }
        }

        return index;
    }
}
