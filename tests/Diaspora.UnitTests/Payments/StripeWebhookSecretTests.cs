using System.Globalization;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using Diaspora.Payments.Stripe;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Payments;

/// <summary>
/// Verifying a Stripe callback when more than one signing secret is legitimate.
/// <para>
/// <b>Stripe issues a secret per endpoint, and this integration registers two.</b> Payment
/// callbacks and payout callbacks arrive on separate routes — deliberately, so an endpoint can be
/// scoped to the events it owns — which means two <c>whsec_</c> values. A verifier that knew only
/// one would refuse every callback from the other as forged, for ever. That is not a noisy failure:
/// a refused payment callback is a deposit that completed at Stripe and never moved the member's
/// balance.
/// </para>
/// <para>
/// The same property covers a secret roll, where Stripe keeps the previous secret live for up to 24
/// hours and signs with both.
/// </para>
/// </summary>
public class StripeWebhookSecretTests
{
    private const string Body = """{"id":"evt_1","type":"checkout.session.completed"}""";

    [Fact]
    public void A_callback_signed_with_the_primary_secret_verifies()
    {
        var settings = Settings("whsec_primary");

        Verify(settings, "whsec_primary").Should().BeTrue();
    }

    [Fact]
    public void A_callback_signed_with_a_second_endpoints_secret_also_verifies()
    {
        // The payouts endpoint's secret. Before this, its every callback was refused.
        var settings = Settings("whsec_primary", "whsec_payouts");

        Verify(settings, "whsec_payouts").Should().BeTrue();
    }

    [Fact]
    public void A_callback_signed_with_a_secret_being_rolled_out_verifies()
    {
        var settings = Settings("whsec_old", "whsec_new");

        Verify(settings, "whsec_old").Should().BeTrue();
        Verify(settings, "whsec_new").Should().BeTrue();
    }

    [Fact]
    public void A_callback_signed_with_a_secret_nobody_configured_is_refused()
    {
        // The direction that matters: accepting more secrets must never mean accepting any secret.
        var settings = Settings("whsec_primary", "whsec_payouts");

        Verify(settings, "whsec_attacker").Should().BeFalse();
    }

    [Fact]
    public void A_deployment_with_no_secret_at_all_verifies_nothing()
    {
        var settings = Settings();

        settings.HasWebhookSecret.Should().BeFalse();
        Verify(settings, "whsec_anything").Should().BeFalse();
    }

    [Fact]
    public void Blank_entries_are_ignored_rather_than_accepted()
    {
        // A documented placeholder left empty must not become a usable secret, and must not make
        // HasWebhookSecret true — that would turn "unverifiable" into "verified".
        var settings = Settings("", "   ");

        settings.AcceptedWebhookSecrets.Should().BeEmpty();
        settings.HasWebhookSecret.Should().BeFalse();
    }

    [Fact]
    public void The_same_secret_configured_twice_is_counted_once()
    {
        var settings = Settings("whsec_same", "whsec_same");

        settings.AcceptedWebhookSecrets.Should().ContainSingle();
    }

    [Fact]
    public void A_stale_timestamp_is_refused_however_correct_the_signature()
    {
        // The other half of the scheme. A correctly signed callback stays correctly signed for
        // ever, so without the age check the HMAC proves origin and nothing about replay.
        var settings = Settings("whsec_primary");
        var now = DateTimeOffset.UnixEpoch.AddDays(1);
        var stale = now.AddSeconds(-settings.WebhookToleranceSeconds - 60);

        var header = SignedHeader("whsec_primary", stale);

        StripePayoutWebhookProcessor.Verify(header, Body, settings, now, out var reason)
            .Should().BeFalse();

        reason.Should().Contain("tolerance");
    }

    [Fact]
    public void The_secrets_bind_from_the_environment_variable_form_a_deployment_actually_uses()
    {
        // The tests above set the property directly, which proves the verifier and nothing about
        // the path an operator takes. Fly supplies these as double-underscore environment
        // variables, and a list binds by index — Payments__Stripe__WebhookSecrets__0. If that
        // binding did not work the deployment would hold one secret while every test passed, and
        // the second endpoint's callbacks would be refused in silence.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Payments:Stripe:SecretKey"] = "sk_test_stub",
                ["Payments:Stripe:WebhookSecret"] = "whsec_payments",
                ["Payments:Stripe:WebhookSecrets:0"] = "whsec_payouts",
                ["Payments:Stripe:WebhookSecrets:1"] = "whsec_rolled",
            })
            .Build();

        var settings = new StripeOptions();
        configuration.GetSection(StripeOptions.SectionName).Bind(settings);

        settings.AcceptedWebhookSecrets
            .Should().BeEquivalentTo(["whsec_payments", "whsec_payouts", "whsec_rolled"]);

        // And each one genuinely verifies a callback signed with it.
        foreach (var secret in settings.AcceptedWebhookSecrets)
        {
            Verify(settings, secret).Should().BeTrue($"a callback signed with {secret} is ours");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static StripeOptions Settings(params string[] secrets) =>
        new()
        {
            SecretKey = "sk_test_stub",
            ApiBase = "https://stripe.test",
            WebhookSecret = secrets.Length > 0 ? secrets[0] : string.Empty,
            WebhookSecrets = [.. secrets.Skip(1)],
        };

    private static bool Verify(StripeOptions settings, string signWith)
    {
        var now = DateTimeOffset.UnixEpoch.AddDays(1);

        return StripePayoutWebhookProcessor.Verify(
            SignedHeader(signWith, now), Body, settings, now, out _);
    }

    private static string SignedHeader(string secret, DateTimeOffset at)
    {
        var timestamp = at.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        var signature = Convert.ToHexString(
                HMACSHA256.HashData(
                    Encoding.UTF8.GetBytes(secret),
                    Encoding.UTF8.GetBytes($"{timestamp}.{Body}")))
            .ToLowerInvariant();

        return $"t={timestamp},v1={signature}";
    }
}
