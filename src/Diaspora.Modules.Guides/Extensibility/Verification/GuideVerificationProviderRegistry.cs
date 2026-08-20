using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Guides.Extensibility.Verification;

/// <summary>
/// Runs the registered verification providers in order and takes the first decision.
/// <para>
/// <b>Isolating each provider is the whole job.</b> A registry that let one provider's
/// exception escape would make an unreachable fact-checking API fail a submission Trust would
/// have handled perfectly well — turning a degraded integration into an outage. Each provider
/// is tried, its failure is logged, and the next one runs.
/// </para>
/// <para>
/// Two providers claiming the same module name fails construction — a registry with two
/// entries it cannot tell apart in a log is a registry nobody can debug. Scoped, not
/// singleton, like every registry in this module (the captive-dependency rule).
/// </para>
/// </summary>
public sealed class GuideVerificationProviderRegistry
{
    private readonly List<IGuideVerificationProvider> _providers;
    private readonly ILogger<GuideVerificationProviderRegistry> _logger;

    public GuideVerificationProviderRegistry(
        IEnumerable<IGuideVerificationProvider> providers,
        ILogger<GuideVerificationProviderRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var ordered = providers.OrderBy(provider => provider.Order).ToList();
        var modules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in ordered)
        {
            if (!modules.Add(provider.Module))
            {
                throw new InvalidOperationException(
                    $"Two guide verification providers claim the module name '{provider.Module}'. " +
                    "A provider's module name must be unique so its decisions can be attributed.");
            }
        }

        _providers = ordered;
        _logger = logger;
    }

    /// <summary>Whether anything at all can decide a claim on this deployment.</summary>
    public bool CanSubmit => _providers.Count > 0;

    /// <summary>
    /// Submits the claim to the first provider that decides it.
    /// <para>
    /// Failing with <c>guides.verification.unavailable</c> when nothing decided is the honest
    /// answer: the alternative — recording the claim as pending with no system actually holding
    /// it — produces a verification that never resolves and that nobody can find to chase.
    /// </para>
    /// </summary>
    public async Task<Result<GuideVerificationOutcome>> SubmitAsync(
        GuideVerificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        foreach (var provider in _providers)
        {
            Result<GuideVerificationOutcome> outcome;

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
                _logger.LogWarning(
                    exception,
                    "Verification provider {Module} threw handling guide {GuideId}. Falling through.",
                    provider.Module,
                    request.GuideId);

                continue;
            }
#pragma warning restore CA1031

            if (outcome.IsFailure)
            {
                _logger.LogWarning(
                    "Verification provider {Module} failed for guide {GuideId}: {Error}. Falling through.",
                    provider.Module,
                    request.GuideId,
                    outcome.Error.Code);

                continue;
            }

            if (outcome.Value.IsDecided)
            {
                return outcome;
            }
        }

        return Result.Failure<GuideVerificationOutcome>(GuideErrors.VerificationUnavailable);
    }
}
