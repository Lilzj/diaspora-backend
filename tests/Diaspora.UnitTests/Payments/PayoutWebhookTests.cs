using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diaspora.Payments.Flutterwave;
using Diaspora.Payments.Stripe;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Payments;

/// <summary>
/// Payout callbacks: what is believed, and what is refused.
/// <para>
/// <b>This is the only anonymous surface on the platform that can complete a payout.</b> A callback
/// that verifies moves a withdrawal to <c>Completed</c> and turns a hold into money that has left,
/// so every test below is about refusing something — a wrong secret, a tampered body, a replayed
/// capture, an event nobody mapped. The one test that accepts is there to prove the rest are not
/// simply refusing everything.
/// </para>
/// </summary>
public class StripePayoutWebhookTests
{
    private const string Secret = "whsec_test_secret";

    [Fact]
    public void A_correctly_signed_recent_callback_verifies()
    {
        var body = Paid("abc123");
        var now = DateTimeOffset.UnixEpoch.AddSeconds(1_700_000_000);

        StripePayoutWebhookProcessor
            .Verify(Header(body, now), body, Settings(), now, out _)
            .Should().BeTrue();
    }

    /// <summary>
    /// <b>The signature is over the body, so altering the body must break it.</b> If it did not, the
    /// header would prove only that somebody once held the secret — and a captured callback could be
    /// re-pointed at another member's withdrawal.
    /// </summary>
    [Fact]
    public void A_tampered_body_does_not_verify()
    {
        var now = DateTimeOffset.UnixEpoch.AddSeconds(1_700_000_000);
        var header = Header(Paid("abc123"), now);

        StripePayoutWebhookProcessor
            .Verify(header, Paid("someone-elses-withdrawal"), Settings(), now, out var reason)
            .Should().BeFalse();

        reason.Should().Contain("no candidate signature matched");
    }

    [Fact]
    public void A_signature_from_another_secret_does_not_verify()
    {
        var body = Paid("abc123");
        var now = DateTimeOffset.UnixEpoch.AddSeconds(1_700_000_000);
        var header = Header(body, now, secret: "whsec_somebody_elses");

        StripePayoutWebhookProcessor
            .Verify(header, body, Settings(), now, out _)
            .Should().BeFalse();
    }

    /// <summary>
    /// <b>A correctly signed callback stays correctly signed for ever, which is why age is checked
    /// separately.</b> Without this, a callback captured off the wire could be replayed months later
    /// and would verify perfectly.
    /// </summary>
    [Fact]
    public void A_callback_older_than_the_tolerance_does_not_verify()
    {
        var body = Paid("abc123");
        var signedAt = DateTimeOffset.UnixEpoch.AddSeconds(1_700_000_000);
        var header = Header(body, signedAt);

        StripePayoutWebhookProcessor
            .Verify(header, body, Settings(), signedAt.AddHours(1), out var reason)
            .Should().BeFalse();

        reason.Should().Contain("tolerance");
    }

    /// <summary>
    /// Ahead as well as behind. A timestamp far in the future is not a clock-drift artefact, it is
    /// somebody choosing one — and accepting it would make the tolerance one-sided and useless.
    /// </summary>
    [Fact]
    public void A_callback_timestamped_in_the_future_does_not_verify()
    {
        var body = Paid("abc123");
        var signedAt = DateTimeOffset.UnixEpoch.AddSeconds(1_700_000_000);
        var header = Header(body, signedAt.AddHours(1));

        StripePayoutWebhookProcessor
            .Verify(header, body, Settings(), signedAt, out _)
            .Should().BeFalse();
    }

    /// <summary>
    /// Stripe sends more than one <c>v1</c> while a signing secret is being rotated. A verifier
    /// reading only the first would reject half of every rotation's callbacks — which looks exactly
    /// like an attack and is not one.
    /// </summary>
    [Fact]
    public void A_second_candidate_signature_is_tried_so_a_secret_can_be_rotated()
    {
        var body = Paid("abc123");
        var now = DateTimeOffset.UnixEpoch.AddSeconds(1_700_000_000);
        var seconds = now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        var header = $"t={seconds},v1={Sign($"{seconds}.{body}", "whsec_the_old_one")},"
            + $"v1={Sign($"{seconds}.{body}", Secret)}";

        StripePayoutWebhookProcessor.Verify(header, body, Settings(), now, out _)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("nonsense")]
    [InlineData("v1=abc")]
    [InlineData("t=1700000000")]
    public void A_malformed_header_does_not_verify(string header)
    {
        var now = DateTimeOffset.UnixEpoch.AddSeconds(1_700_000_000);

        StripePayoutWebhookProcessor
            .Verify(header, Paid("abc123"), Settings(), now, out _)
            .Should().BeFalse();
    }

    // ── Translation ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("transfer.paid", "paid")]
    [InlineData("transfer.failed", "failed")]
    [InlineData("transfer.reversed", "reversed")]
    public void The_three_transfer_events_map_to_the_three_outcomes(string type, string expected)
    {
        var result = StripePayoutWebhookProcessor.Translate(Event(type, "abc123"));

        result.IsRecognized.Should().BeTrue();
        result.EventKind.Should().Be(expected);
        result.WithdrawalReference.Should().Be("abc123");
        result.EventId.Should().Be("evt_1");
    }

    /// <summary>
    /// <b>An event nobody mapped is acknowledged and ignored, never acted on.</b> A processor that
    /// guessed at an unfamiliar shape would be acting on a payload nobody checked.
    /// </summary>
    [Theory]
    [InlineData("transfer.created")]
    [InlineData("payout.paid")]
    [InlineData("account.updated")]
    [InlineData("charge.succeeded")]
    public void An_unmapped_event_is_ignored(string type)
    {
        var result = StripePayoutWebhookProcessor.Translate(Event(type, "abc123"));

        result.IsVerified.Should().BeTrue();
        result.IsRecognized.Should().BeFalse("nothing should be applied from it");
    }

    [Fact]
    public void A_transfer_with_no_transfer_group_is_ignored()
    {
        const string Body =
            """{"id":"evt_1","type":"transfer.paid","data":{"object":{"id":"tr_1"}}}""";

        StripePayoutWebhookProcessor.Translate(Body).IsRecognized.Should().BeFalse();
    }

    [Fact]
    public void An_unparseable_body_is_ignored_rather_than_thrown()
    {
        StripePayoutWebhookProcessor.Translate("<html>").IsRecognized.Should().BeFalse();
    }

    [Fact]
    public void A_failure_carries_stripes_own_wording_for_the_operator()
    {
        const string Body =
            """
            {"id":"evt_1","type":"transfer.failed","data":{"object":{"id":"tr_1",
             "transfer_group":"abc123","failure_code":"account_closed",
             "failure_message":"The bank account has been closed."}}}
            """;

        var result = StripePayoutWebhookProcessor.Translate(Body);

        result.FailureCode.Should().Be("account_closed");
        result.FailureMessage.Should().Contain("closed");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────

    private static StripeOptions Settings() =>
        new() { SecretKey = "sk_test_x", WebhookSecret = Secret, WebhookToleranceSeconds = 300 };

    private static string Paid(string reference) => Event("transfer.paid", reference);

    private static string Event(string type, string reference) =>
        "{\"id\":\"evt_1\",\"type\":\"" + type + "\",\"data\":{\"object\":{\"id\":\"tr_1\","
        + "\"transfer_group\":\"" + reference + "\"}}}";

    private static string Header(string body, DateTimeOffset at, string secret = Secret)
    {
        var seconds = at.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        return $"t={seconds},v1={Sign($"{seconds}.{body}", secret)}";
    }

    private static string Sign(string payload, string secret) =>
        Convert.ToHexString(
                HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();
}

/// <summary>
/// Flutterwave's callbacks, which authenticate with a shared secret rather than a signature.
/// <para>
/// That is materially weaker — the header proves the sender knows the secret and says nothing about
/// the body — and it is the provider's design rather than a choice available here. These pin what it
/// does still refuse.
/// </para>
/// </summary>
public class FlutterwavePayoutWebhookTests
{
    [Theory]
    [InlineData("SUCCESSFUL", "paid")]
    [InlineData("FAILED", "failed")]
    [InlineData("REVERSED", "reversed")]
    public void A_completed_transfer_maps_by_its_status(string status, string expected)
    {
        var result = FlutterwavePayoutWebhookProcessor.Translate(Completed(status));

        result.IsRecognized.Should().BeTrue();
        result.EventKind.Should().Be(expected);
        result.ProviderReference.Should().Be("98765");
    }

    /// <summary>
    /// <b>A transfer still moving is not an outcome.</b> Mapping <c>NEW</c> or <c>PENDING</c> would
    /// drive a withdrawal to a terminal state on a stage Flutterwave has not finished.
    /// </summary>
    [Theory]
    [InlineData("NEW")]
    [InlineData("PENDING")]
    [InlineData("SOMETHING_NEW_IN_2027")]
    public void A_transfer_that_has_not_finished_is_ignored(string status)
    {
        FlutterwavePayoutWebhookProcessor.Translate(Completed(status))
            .IsRecognized.Should().BeFalse();
    }

    [Fact]
    public void Another_event_is_ignored()
    {
        const string Body = """{"event":"charge.completed","data":{"reference":"x","status":"SUCCESSFUL"}}""";

        FlutterwavePayoutWebhookProcessor.Translate(Body).IsRecognized.Should().BeFalse();
    }

    /// <summary>
    /// Flutterwave echoes back the reference the transfer was created with, and the submission
    /// namespaces it — so unlike Stripe's `transfer_group`, this one really does need unwrapping
    /// before it will parse as a withdrawal identifier.
    /// </summary>
    [Fact]
    public void The_namespaced_reference_is_unwrapped_to_the_withdrawal_identifier()
    {
        FlutterwavePayoutWebhookProcessor.Unprefix("diaspora-payout-abc123").Should().Be("abc123");
        FlutterwavePayoutWebhookProcessor.Unprefix("abc123").Should().Be("abc123");
    }

    [Fact]
    public void An_unparseable_body_is_ignored_rather_than_thrown()
    {
        FlutterwavePayoutWebhookProcessor.Translate("not json").IsRecognized.Should().BeFalse();
    }

    private static string Completed(string status) =>
        "{\"event\":\"transfer.completed\",\"data\":{\"id\":98765,"
        + "\"reference\":\"diaspora-payout-abc123\",\"status\":\"" + status + "\"}}";
}
