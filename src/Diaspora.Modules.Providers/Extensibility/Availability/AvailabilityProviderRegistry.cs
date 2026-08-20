using Diaspora.Modules.Providers.Observability;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Providers.Extensibility.Availability;

/// <summary>
/// Runs the registered availability providers in order and takes the first decisive answer.
/// <para>
/// Isolates each one for the same reason the verification registry does: a booking integration
/// that is briefly unreachable must degrade to the weekly schedule, not blank the open-now
/// indicator across the whole directory.
/// </para>
/// <para>
/// Scoped, like every other registry in this module — see
/// <c>ProviderVerificationProviderRegistry</c> for why a singleton over scoped implementations
/// is the captive dependency the host refuses to start on.
/// </para>
/// </summary>
public sealed class AvailabilityProviderRegistry
{
    private readonly IReadOnlyList<IProviderAvailabilityProvider> _providers;
    private readonly ILogger<AvailabilityProviderRegistry> _logger;

    public AvailabilityProviderRegistry(
        IEnumerable<IProviderAvailabilityProvider> providers,
        ILogger<AvailabilityProviderRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _providers = providers.OrderBy(provider => provider.Order).ToList();
        _logger = logger;
    }

    public async Task<AvailabilityAnswer> EvaluateAsync(
        AvailabilityQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        foreach (var provider in _providers)
        {
            if (!provider.AppliesTo(query.Provider))
            {
                continue;
            }

            try
            {
                var answer = await provider.EvaluateAsync(query, cancellationToken);

                if (answer.IsDecided)
                {
                    return answer;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // One provider's outage must not blank the open-now indicator.
            catch (Exception exception)
            {
                ProviderTelemetry.AvailabilityProviderFailures.Add(
                    1, new KeyValuePair<string, object?>("module", provider.Module));

                _logger.LogWarning(
                    exception,
                    "Availability provider {Module} threw for provider {ProviderId}. Falling through.",
                    provider.Module,
                    query.Provider.Id.Value);
            }
#pragma warning restore CA1031
        }

        // Nothing decided. Saying nothing is right: a client that receives null renders no
        // open-now chip, where a fabricated "Closed" would cost the provider business.
        return AvailabilityAnswer.Unknown;
    }
}
