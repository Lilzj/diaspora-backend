using Microsoft.Extensions.Options;

namespace Diaspora.Sms;

/// <summary>
/// Refuses to start a host whose SMS configuration cannot do what it claims.
/// <para>
/// Every rule here catches a misconfiguration whose runtime symptom is a member not receiving a
/// code — which surfaces as "the app is broken", days later, from somebody who cannot get in to
/// report it. A startup message naming the key is worth a great deal more than that.
/// </para>
/// <para>
/// <b>What it deliberately does not do</b> is refuse a host that has no SMS provider at all. That
/// is a legitimate development configuration, and the placeholder behind the port handles it —
/// see <see cref="SmsAdapterRegistration"/>, which does not replace the placeholder unless
/// something is configured.
/// </para>
/// </summary>
public sealed class SmsOptionsValidator : IValidateOptions<SmsOptions>
{
    public ValidateOptionsResult Validate(string? name, SmsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.DefaultProvider))
        {
            // Routes cover the destinations somebody thought of. The default carries everybody
            // else, and on a global platform that is most of the world.
            failures.Add(
                $"'{SmsOptions.SectionName}:DefaultProvider' is required once any SMS provider is "
                + "configured. Without it, only destinations matched by an explicit route can be "
                + "reached.");
        }
        else if (!IsKnown(options.DefaultProvider))
        {
            failures.Add(Unknown(options.DefaultProvider, $"{SmsOptions.SectionName}:DefaultProvider"));
        }
        else if (!IsCredentialed(options, options.DefaultProvider))
        {
            failures.Add(Uncredentialed(options.DefaultProvider));
        }

        for (var index = 0; index < options.Routes.Count; index++)
        {
            var route = options.Routes[index];
            var path = $"{SmsOptions.SectionName}:Routes:{index}";

            if (string.IsNullOrWhiteSpace(route.Provider))
            {
                failures.Add($"'{path}:Provider' is required.");
                continue;
            }

            if (!IsKnown(route.Provider))
            {
                failures.Add(Unknown(route.Provider, $"{path}:Provider"));
                continue;
            }

            if (!IsCredentialed(options, route.Provider))
            {
                failures.Add(Uncredentialed(route.Provider));
            }

            if (route.CallingCodes.Count == 0)
            {
                // A route that matches nothing is not harmless: it reads as coverage that exists,
                // and the destinations it was meant to claim silently go to the default provider
                // at the default provider's price.
                failures.Add(
                    $"'{path}:CallingCodes' is empty, so this route can never match. Remove it or "
                    + "list the prefixes it should carry.");
            }

            foreach (var code in route.CallingCodes)
            {
                if (string.IsNullOrWhiteSpace(code) || !HasDigit(code))
                {
                    failures.Add($"'{path}:CallingCodes' contains '{code}', which has no digits.");
                }
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsKnown(string provider) =>
        string.Equals(provider, SmsProviderNames.Twilio, StringComparison.OrdinalIgnoreCase)
        || string.Equals(provider, SmsProviderNames.AfricasTalking, StringComparison.OrdinalIgnoreCase);

    private static bool IsCredentialed(SmsOptions options, string provider) =>
        string.Equals(provider, SmsProviderNames.Twilio, StringComparison.OrdinalIgnoreCase)
            ? options.Twilio.IsConfigured
            : options.AfricasTalking.IsConfigured;

    private static bool HasDigit(string value)
    {
        foreach (var character in value)
        {
            if (char.IsAsciiDigit(character))
            {
                return true;
            }
        }

        return false;
    }

    private static string Unknown(string provider, string path) =>
        $"'{path}' is '{provider}', which is not a provider this build carries. "
        + $"Use '{SmsProviderNames.Twilio}' or '{SmsProviderNames.AfricasTalking}'.";

    private static string Uncredentialed(string provider) =>
        $"Provider '{provider}' is selected but '{SmsOptions.SectionName}:{provider}' is missing "
        + "credentials, so every message routed to it would fail.";
}
