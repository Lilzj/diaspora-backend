using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Diaspora.Api.RateLimiting;

/// <summary>
/// Refuses to start a deployed host with rate limiting turned off.
/// <para>
/// The switch exists for the integration suite, which drives dozens of sign-ins from one
/// address in a second and is not the traffic the limiter defends against. Left off in a
/// deployed environment it removes the only bound on credential stuffing, SMS pumping and
/// token-refresh abuse — and it would do so silently, which is exactly the failure mode
/// B2.6 exists to stop.
/// </para>
/// </summary>
public sealed class RateLimitingOptionsValidator : IValidateOptions<RateLimitingOptions>
{
    private readonly IHostEnvironment _environment;

    public RateLimitingOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, RateLimitingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Enabled || _environment.IsDevelopment() || _environment.IsEnvironment("Testing"))
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            $"'{RateLimitingOptions.SectionName}:Enabled' cannot be false in the " +
            $"'{_environment.EnvironmentName}' environment. It is the only bound on " +
            "credential stuffing and one-time-code abuse.");
    }
}
