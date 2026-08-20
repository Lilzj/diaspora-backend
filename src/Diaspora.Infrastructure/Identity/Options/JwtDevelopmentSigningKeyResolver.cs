using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Diaspora.Infrastructure.Identity.Options;

/// <summary>
/// Materialises the development signing key into <see cref="JwtOptions"/> when the
/// configuration leaves it blank and the host is running in Development.
/// <para>
/// <b>This exists so there is exactly one resolved signing key.</b> The fallback used to live
/// in the API's bearer configurator, which meant the key was substituted on the half of the
/// feature that <em>validates</em> a token and not on the half that <em>issues</em> one:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <see cref="JwtOptionsValidator"/> permitted a blank key in Development, so the host started.
///   </description></item>
///   <item><description>
///     The bearer configurator substituted a development key, so an incoming token validated.
///   </description></item>
///   <item><description>
///     <c>JwtAccessTokenIssuer</c> read <see cref="JwtOptions.SigningKey"/> raw and threw
///     <c>IDX10703: key length is zero</c> — so no token could ever be issued in the one
///     environment where the key was allowed to be blank.
///   </description></item>
/// </list>
/// <para>
/// Running as an <see cref="IPostConfigureOptions{TOptions}"/> puts the substitution before
/// validation and before every consumer, so the issuer, the validator, the service-token
/// issuer and the OTP code generator all read the same resolved value. Adding a second
/// fallback anywhere else would recreate exactly the defect this removes.
/// </para>
/// <para>
/// <b>It does nothing outside Development.</b> A blank key in any other environment reaches
/// <see cref="JwtOptionsValidator"/> unchanged and stops the host, which is the behaviour that
/// must not be weakened: a service that starts without a real key is a service issuing
/// forgeable tokens.
/// </para>
/// </summary>
public sealed class JwtDevelopmentSigningKeyResolver : IPostConfigureOptions<JwtOptions>
{
    private readonly IHostEnvironment _environment;

    public JwtDevelopmentSigningKeyResolver(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _environment = environment;
    }

    public void PostConfigure(string? name, JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.SigningKey)) return;
        if (!_environment.IsDevelopment()) return;

        options.SigningKey = JwtOptions.DevelopmentSigningKey;
    }
}
