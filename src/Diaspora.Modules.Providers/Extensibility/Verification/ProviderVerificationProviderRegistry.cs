using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Observability;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Providers.Extensibility.Verification;

/// <summary>
/// Runs the registered verification providers in order and takes the first decision.
/// <para>
/// <b>Isolating each provider is the whole job.</b> A registry that let one provider's exception
/// escape would make an unreachable companies-register API fail a verification submission that
/// Trust would have handled perfectly well — turning a degraded integration into an outage. Each
/// provider is tried, its failure is logged and counted, and the next one runs.
/// </para>
/// <para>
/// Scoped, not singleton, and deliberately: nearly every real provider will hold a repository or
/// an HTTP client with per-request state, and a singleton registry over scoped implementations
/// is the captive dependency the host's provider validation refuses to start on. B4 recorded
/// exactly that failure with its country metadata catalogue, and B6 wrote the same note over its
/// own registries. Constructing one dictionary per request is a small price for not repeating it.
/// </para>
/// </summary>
public sealed class ProviderVerificationProviderRegistry
{
    private readonly IReadOnlyList<IProviderVerificationProvider> _providers;
    private readonly ILogger<ProviderVerificationProviderRegistry> _logger;

    public ProviderVerificationProviderRegistry(
        IEnumerable<IProviderVerificationProvider> providers,
        ILogger<ProviderVerificationProviderRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _providers = providers.OrderBy(provider => provider.Order).ToList();
        _logger = logger;
    }

    /// <summary>Whether anything at all can handle this kind on this deployment.</summary>
    public bool CanHandle(ProviderVerificationKind kind) =>
        _providers.Any(provider => provider.Handles(kind));

    /// <summary>
    /// Submits the claim to the first provider that decides it.
    /// <para>
    /// Failing with <c>providers.verification.provider-unavailable</c> when nothing decided is
    /// the honest answer: the alternative — recording the claim as pending with no system
    /// actually holding it — produces a verification that never resolves and that nobody can
    /// find to chase.
    /// </para>
    /// </summary>
    public async Task<Result<ProviderVerificationOutcome>> SubmitAsync(
        ProviderVerificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        foreach (var provider in _providers)
        {
            if (!provider.Handles(request.Kind))
            {
                continue;
            }

            Result<ProviderVerificationOutcome> outcome;

            try
            {
                outcome = await provider.SubmitAsync(request, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // One provider's outage must not fail the submission. See the summary.
            catch (Exception exception)
            {
                ProviderTelemetry.VerificationProviderFailures.Add(
                    1,
                    new KeyValuePair<string, object?>("module", provider.Module),
                    new KeyValuePair<string, object?>("kind", request.Kind.ToString()));

                _logger.LogWarning(
                    exception,
                    "Verification provider {Module} threw handling {Kind} for provider {ProviderId}. Falling through.",
                    provider.Module,
                    request.Kind,
                    request.ProviderId);

                continue;
            }
#pragma warning restore CA1031

            if (outcome.IsFailure)
            {
                ProviderTelemetry.VerificationProviderFailures.Add(
                    1,
                    new KeyValuePair<string, object?>("module", provider.Module),
                    new KeyValuePair<string, object?>("kind", request.Kind.ToString()));

                continue;
            }

            if (outcome.Value.IsDecided)
            {
                return outcome;
            }
        }

        return Result.Failure<ProviderVerificationOutcome>(
            ProviderErrors.VerificationProviderUnavailable);
    }
}
