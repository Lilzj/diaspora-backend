using Diaspora.Domain.Identity.Errors;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Authentication;

/// <inheritdoc cref="IAuthenticationProviderRegistry" />
public sealed class AuthenticationProviderRegistry : IAuthenticationProviderRegistry
{
    private readonly Dictionary<string, IAuthenticationProvider> _byMethod;

    public AuthenticationProviderRegistry(IEnumerable<IAuthenticationProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _byMethod = providers.ToDictionary(
            provider => provider.Method.Key,
            StringComparer.Ordinal);
    }

    public IReadOnlyCollection<AuthenticationMethod> AvailableMethods =>
        _byMethod.Values.Select(provider => provider.Method).ToArray();

    public bool Supports(AuthenticationMethod method) => _byMethod.ContainsKey(method.Key);

    public Result<IAuthenticationProvider> Resolve(AuthenticationMethod method) =>
        _byMethod.TryGetValue(method.Key, out var provider)
            ? Result.Success(provider)
            : Result.Failure<IAuthenticationProvider>(UserErrors.AuthenticationMethodNotSupported(method.Key));
}
