using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Diaspora.Infrastructure.Identity.Options;

/// <summary>
/// Startup validation for the signing configuration.
/// <para>
/// These checks run at boot and stop the host rather than surfacing later as a runtime
/// failure — a service that starts with a weak or absent signing key is a service issuing
/// forgeable tokens, and it should never get as far as accepting a request.
/// </para>
/// <para>
/// Development is allowed a generated key so a new contributor can clone and run. Every
/// other environment must supply one from a secret store.
/// </para>
/// </summary>
public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    private readonly IHostEnvironment _environment;

    public JwtOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            if (!_environment.IsDevelopment())
            {
                failures.Add(
                    $"'{JwtOptions.SectionName}:SigningKey' must be configured outside Development. " +
                    "Supply it from a secret store, never from appsettings.json.");
            }
        }
        else
        {
            var keyBytes = Encoding.UTF8.GetByteCount(options.SigningKey);
            if (keyBytes < JwtOptions.MinimumSigningKeyBytes)
            {
                failures.Add(
                    $"'{JwtOptions.SectionName}:SigningKey' is {keyBytes} bytes; " +
                    $"HMAC-SHA256 requires at least {JwtOptions.MinimumSigningKeyBytes}.");
            }
        }

        if (!Uri.TryCreate(options.Issuer, UriKind.Absolute, out _))
        {
            failures.Add($"'{JwtOptions.SectionName}:Issuer' must be an absolute URI.");
        }

        if (!Uri.TryCreate(options.Audience, UriKind.Absolute, out _))
        {
            failures.Add($"'{JwtOptions.SectionName}:Audience' must be an absolute URI.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
