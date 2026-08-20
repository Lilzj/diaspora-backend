using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Extensibility.Intelligence;

/// <summary>
/// Picks the provider that will handle a verification claim.
/// <para>
/// <b>First match by order wins, and there is no fallback chain.</b> A claim goes to one provider,
/// not to every provider that might have an opinion: raising the same claim with two systems produces
/// two cases, two reviewers and eventually two contradictory decisions arriving out of order at one
/// projection.
/// </para>
/// <para>
/// <b>Scoped, like every registry on this module.</b> A provider holds an HTTP client or a repository,
/// and a singleton registry over scoped implementations is the captive dependency the host's provider
/// validation refuses to start on. B4 recorded that failure, and B6, B7, B8 and B8.1 each repeated the
/// note; paying one enumeration per request is a small price for not repeating it a sixth time.
/// </para>
/// <para>
/// With no provider registered at all — a deployment running without Trust — the answer is a service
/// unavailable failure rather than a silent success. Leaving an unverified authority looking verified
/// is the one outcome that would make every "official" badge on the platform meaningless.
/// </para>
/// </summary>
public sealed class AuthorityVerificationProviderRegistry
{
    private readonly IReadOnlyList<IAuthorityVerificationProvider> _providers;

    public AuthorityVerificationProviderRegistry(
        IEnumerable<IAuthorityVerificationProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _providers = [.. providers.OrderBy(provider => provider.Order)];
    }

    /// <summary>The registered providers, in the order they are consulted. For diagnostics.</summary>
    public IReadOnlyList<string> ProviderNames => [.. _providers.Select(provider => provider.Module)];

    /// <summary>Whether anything at all can verify an authority on this deployment.</summary>
    public bool HasProvider => _providers.Count > 0;

    public async Task<Result<AuthorityVerificationOutcome>> SubmitAsync(
        AuthorityVerificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var provider = _providers.FirstOrDefault(
            candidate => candidate.Handles(request.CountryCode));

        return provider is null
            ? Result.Failure<AuthorityVerificationOutcome>(
                IntelligenceErrors.VerificationUnavailable)
            : await provider.SubmitAsync(request, cancellationToken);
    }
}
