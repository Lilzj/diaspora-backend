using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Authentication;

/// <summary>
/// Resolves the provider for a requested authentication method.
/// <para>
/// Built from every <see cref="IAuthenticationProvider"/> in the container, so registering
/// a new provider is the whole of adding a new sign-in method. Nothing enumerates a closed
/// list of methods; the registry reports what is actually installed.
/// </para>
/// </summary>
public interface IAuthenticationProviderRegistry
{
    /// <summary>The methods this host can authenticate with, for discovery and diagnostics.</summary>
    IReadOnlyCollection<AuthenticationMethod> AvailableMethods { get; }

    bool Supports(AuthenticationMethod method);

    /// <summary>
    /// The provider for a method, or a failure when nothing is registered for it — which is
    /// a configuration problem (a client asked for a method this deployment does not offer),
    /// not a credential problem.
    /// </summary>
    Result<IAuthenticationProvider> Resolve(AuthenticationMethod method);
}
