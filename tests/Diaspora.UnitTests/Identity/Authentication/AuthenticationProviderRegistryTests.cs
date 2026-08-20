using Diaspora.Application.Identity.Authentication;
using Diaspora.Domain.Identity.Users;
using Diaspora.SharedKernel.Results;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Identity.Authentication;

/// <summary>
/// The registry is what makes sign-in methods pluggable: it resolves by method key, and a
/// method nobody registered is a configuration failure, not a credential failure.
/// </summary>
public class AuthenticationProviderRegistryTests
{
    [Fact]
    public void It_reports_the_methods_that_are_actually_registered()
    {
        var registry = new AuthenticationProviderRegistry(
            [new StubProvider(AuthenticationMethod.Password), new StubProvider(AuthenticationMethod.PhoneOtp)]);

        registry.AvailableMethods.Should().BeEquivalentTo(
            [AuthenticationMethod.Password, AuthenticationMethod.PhoneOtp]);
    }

    [Fact]
    public void It_resolves_a_registered_method()
    {
        var registry = new AuthenticationProviderRegistry([new StubProvider(AuthenticationMethod.PhoneOtp)]);

        var resolved = registry.Resolve(AuthenticationMethod.PhoneOtp);

        resolved.IsSuccess.Should().BeTrue();
        resolved.Value.Method.Should().Be(AuthenticationMethod.PhoneOtp);
    }

    [Fact]
    public void An_unregistered_method_resolves_to_a_configuration_failure()
    {
        // Password is not installed on this host. Asking for it is a validation-shaped
        // error, not "wrong credentials" — the client asked for something the deployment
        // does not offer.
        var registry = new AuthenticationProviderRegistry([new StubProvider(AuthenticationMethod.PhoneOtp)]);

        var resolved = registry.Resolve(AuthenticationMethod.Password);

        resolved.IsFailure.Should().BeTrue();
        resolved.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void A_brand_new_method_needs_no_change_to_the_registry()
    {
        // The extensibility guarantee: a provider with a method key nobody has seen before
        // is resolvable purely by being in the set handed to the registry.
        var google = new AuthenticationMethod("google");
        var registry = new AuthenticationProviderRegistry([new StubProvider(google)]);

        registry.Supports(google).Should().BeTrue();
        registry.Resolve(google).IsSuccess.Should().BeTrue();
    }

    private sealed class StubProvider : IAuthenticationProvider
    {
        public StubProvider(AuthenticationMethod method) => Method = method;

        public AuthenticationMethod Method { get; }

        public Task<Result<AuthenticatedMember>> AuthenticateAsync(
            AuthenticationCredentials credentials, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure<AuthenticatedMember>(
                Diaspora.Domain.Identity.Errors.UserErrors.InvalidCredentials));
    }
}
