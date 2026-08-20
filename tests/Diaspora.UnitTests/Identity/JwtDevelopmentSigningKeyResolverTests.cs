using Diaspora.Infrastructure.Identity.Options;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Diaspora.UnitTests.Identity;

/// <summary>
/// DEFECT-001. The signing key must be resolved once, before anything reads it.
///
/// <para>
/// <b>The failure this prevents was silent at startup and total at runtime.</b> A blank
/// <c>Identity:Jwt:SigningKey</c> was permitted in Development by <see cref="JwtOptionsValidator"/>
/// and substituted by the API's bearer configurator, so the host started and an incoming token
/// validated — but <c>JwtAccessTokenIssuer</c> read the raw empty string and threw
/// <c>IDX10703: key length is zero</c>. Development was the one environment where the key was
/// allowed to be blank and the one environment where no token could ever be issued, so on a
/// fresh clone nobody could sign in and the entire authenticated surface was unreachable.
/// </para>
///
/// <para>
/// The regression these tests guard is not "the key is non-empty". It is that <b>the same
/// resolved value reaches every consumer</b>. Anything that reintroduces a second fallback —
/// in the bearer configurator, in an issuer, in the OTP generator — puts the two halves back in
/// a position to disagree.
/// </para>
/// </summary>
public sealed class JwtDevelopmentSigningKeyResolverTests
{
    private static JwtOptions Resolve(string environmentName, string configured)
    {
        var options = new JwtOptions { SigningKey = configured };
        var resolver = new JwtDevelopmentSigningKeyResolver(new StubEnvironment(environmentName));
        resolver.PostConfigure(name: null, options);
        return options;
    }

    private static (bool Valid, string Failures) Validate(string environmentName, JwtOptions options)
    {
        var result = new JwtOptionsValidator(new StubEnvironment(environmentName)).Validate(null, options);
        return (result.Succeeded, string.Join(" | ", result.Failures ?? []));
    }

    [Fact]
    public void Development_with_a_blank_key_resolves_to_the_development_key()
    {
        var options = Resolve(Environments.Development, configured: "");

        options.SigningKey.Should().Be(JwtOptions.DevelopmentSigningKey);
        options.SigningKey.Should().NotBeEmpty(
            "an empty key is what made JwtAccessTokenIssuer throw IDX10703");
    }

    [Fact]
    public void Development_with_a_blank_key_then_passes_validation()
    {
        // The resolver runs as a post-configure, so validation sees the resolved value.
        // This is the property that makes the key single-source-of-truth rather than a
        // second opinion held by one consumer.
        var options = Resolve(Environments.Development, configured: "");

        Validate(Environments.Development, options).Valid.Should().BeTrue();
    }

    [Fact]
    public void Development_never_overrides_a_configured_key()
    {
        const string configured = "a-real-development-key-that-is-long-enough-for-hmac-sha256";

        Resolve(Environments.Development, configured).SigningKey.Should().Be(configured);
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void Outside_development_a_blank_key_is_not_substituted(string environmentName)
    {
        // The substitution must not travel. A deployed environment that quietly acquired a
        // committed key would be a deployment issuing forgeable tokens.
        Resolve(environmentName, configured: "").SigningKey.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void Outside_development_a_blank_key_still_stops_the_host(string environmentName)
    {
        var options = Resolve(environmentName, configured: "");

        var (valid, failures) = Validate(environmentName, options);

        valid.Should().BeFalse();
        failures.Should().Contain("must be configured outside Development");
    }

    [Fact]
    public void The_development_key_is_long_enough_for_hmac_sha256()
    {
        // A resolved key that failed the length floor would move the failure from the issuer
        // to the validator rather than removing it.
        System.Text.Encoding.UTF8.GetByteCount(JwtOptions.DevelopmentSigningKey)
            .Should().BeGreaterThanOrEqualTo(JwtOptions.MinimumSigningKeyBytes);
    }

    private sealed class StubEnvironment : IHostEnvironment
    {
        public StubEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Diaspora.Tests";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
