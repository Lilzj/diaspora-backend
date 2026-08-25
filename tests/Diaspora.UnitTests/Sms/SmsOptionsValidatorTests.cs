using Diaspora.Sms;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Sms;

/// <summary>
/// Configuration that cannot do what it claims, refused at startup.
/// <para>
/// Every rule here has the same runtime symptom if it is not caught: a member does not receive a
/// code. That symptom arrives days later, from somebody who by definition cannot sign in to
/// report it, and it is indistinguishable from a provider outage. A startup message naming the
/// key is worth a great deal more.
/// </para>
/// </summary>
public sealed class SmsOptionsValidatorTests
{
    [Fact]
    public void A_configured_deployment_validates()
    {
        var result = Validate(new SmsOptions
        {
            DefaultProvider = SmsProviderNames.Twilio,
            Twilio = { AccountSid = "AC1", AuthToken = "t", From = "+441234567890" },
            AfricasTalking = { Username = "diaspora", ApiKey = "k" },
            Routes = [new SmsRouteOptions
            {
                Provider = SmsProviderNames.AfricasTalking,
                CallingCodes = ["+234", "+254"]
            }]
        });

        result.Failed.Should().BeFalse();
    }

    [Fact]
    public void A_missing_default_provider_is_refused()
    {
        // Routes cover the destinations somebody thought of. The default carries everybody else,
        // and on a platform for people living abroad that is most of the world.
        var result = Validate(new SmsOptions
        {
            Twilio = { AccountSid = "AC1", AuthToken = "t", From = "+441234567890" }
        });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("DefaultProvider");
    }

    [Fact]
    public void A_provider_this_build_does_not_carry_is_refused()
    {
        // A typo, or a provider somebody added to a config file before adding the adapter. Left
        // to run, every message routed to it fails at the wire.
        var result = Validate(new SmsOptions { DefaultProvider = "Termii" });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Termii");
    }

    [Fact]
    public void A_selected_provider_with_no_credentials_is_refused()
    {
        var result = Validate(new SmsOptions { DefaultProvider = SmsProviderNames.Twilio });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("credentials");
    }

    [Fact]
    public void A_route_with_no_calling_codes_is_refused()
    {
        // Not harmless: it reads as coverage that exists, and the destinations it was meant to
        // claim quietly go to the default provider at the default provider's price.
        var result = Validate(new SmsOptions
        {
            DefaultProvider = SmsProviderNames.Twilio,
            Twilio = { AccountSid = "AC1", AuthToken = "t", From = "+441234567890" },
            AfricasTalking = { Username = "diaspora", ApiKey = "k" },
            Routes = [new SmsRouteOptions { Provider = SmsProviderNames.AfricasTalking }]
        });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("CallingCodes");
    }

    [Fact]
    public void A_calling_code_with_no_digits_is_refused()
    {
        var result = Validate(new SmsOptions
        {
            DefaultProvider = SmsProviderNames.Twilio,
            Twilio = { AccountSid = "AC1", AuthToken = "t", From = "+441234567890" },
            AfricasTalking = { Username = "diaspora", ApiKey = "k" },
            Routes = [new SmsRouteOptions
            {
                Provider = SmsProviderNames.AfricasTalking,
                CallingCodes = ["Nigeria"]
            }]
        });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Nigeria");
    }

    [Fact]
    public void A_route_to_a_provider_with_no_credentials_is_refused()
    {
        // The router would fall through to the default wire and keep working, which is the right
        // runtime behaviour — and exactly why this must be caught at startup instead. Silently
        // paying the expensive provider for every Nigerian number is a bill, not an error.
        var result = Validate(new SmsOptions
        {
            DefaultProvider = SmsProviderNames.Twilio,
            Twilio = { AccountSid = "AC1", AuthToken = "t", From = "+441234567890" },
            Routes = [new SmsRouteOptions
            {
                Provider = SmsProviderNames.AfricasTalking,
                CallingCodes = ["+234"]
            }]
        });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(SmsProviderNames.AfricasTalking);
    }

    private static Microsoft.Extensions.Options.ValidateOptionsResult Validate(SmsOptions options) =>
        new SmsOptionsValidator().Validate(null, options);
}
