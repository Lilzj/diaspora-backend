using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Diaspora.Infrastructure.Identity.Options;

/// <summary>
/// Refuses to start a non-Development host that would write one-time codes to the log.
/// <para>
/// The flag exists so a developer can complete registration without an SMS provider. Left
/// on in a deployed environment it turns the application log — routinely shipped to
/// aggregators and readable by people who should never see a credential — into a live feed
/// of every verification and recovery code the platform issues.
/// </para>
/// </summary>
public sealed class IdentityModuleOptionsValidator : IValidateOptions<IdentityModuleOptions>
{
    private readonly IHostEnvironment _environment;

    public IdentityModuleOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, IdentityModuleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.LogOtpCodesForDevelopment && !_environment.IsDevelopment())
        {
            return ValidateOptionsResult.Fail(
                $"'{IdentityModuleOptions.SectionName}:LogOtpCodesForDevelopment' cannot be enabled in the " +
                $"'{_environment.EnvironmentName}' environment. It writes live one-time codes to the " +
                "application log.");
        }

        return ValidateOptionsResult.Success;
    }
}
