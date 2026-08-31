using System.Text.Json;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Diaspora.Payments.Flutterwave;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Payments;

/// <summary>
/// The request the Flutterwave adapter builds, and — the part that matters most — what it refuses to
/// read as a success.
/// <para>
/// Like the PayPal tests, these pin a payload written from documentation rather than one executed
/// against a live account. Capability rows stay <c>Unknown</c> until the live sandbox test has run.
/// </para>
/// </summary>
public class FlutterwaveSettlementTests
{
    [Fact]
    public void A_transfer_carries_the_bank_the_account_and_the_amount()
    {
        var body = FlutterwaveSettlementProvider.BuildTransfer(
            Instruction(), "044", "0690000031", debitCurrency: "NGN");

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        root.GetProperty("account_bank").GetString().Should().Be("044");
        root.GetProperty("account_number").GetString().Should().Be("0690000031");
        root.GetProperty("amount").GetString().Should().Be("10.00");
        root.GetProperty("currency").GetString().Should().Be("NGN");
        root.GetProperty("reference").GetString().Should().Be("diaspora-payout-abc123");
        root.GetProperty("debit_currency").GetString().Should().Be("NGN");
    }

    [Fact]
    public void An_unset_debit_currency_is_left_out_rather_than_sent_empty()
    {
        var body = FlutterwaveSettlementProvider.BuildTransfer(
            Instruction(), "044", "0690000031", debitCurrency: "");

        using var document = JsonDocument.Parse(body);

        document.RootElement.TryGetProperty("debit_currency", out _).Should().BeFalse(
            "an empty string is a value Flutterwave would try to interpret");
    }

    /// <summary>
    /// A transfer names a bank and an account within it; a payout destination's token is one string.
    /// A token in any other shape is refused rather than improvised — an account number with no bank
    /// produces a transfer to whatever Flutterwave decides that means.
    /// </summary>
    [Theory]
    [InlineData("044:0690000031", "044", "0690000031")]
    [InlineData(" 044 : 0690000031 ", "044", "0690000031")]
    public void A_destination_token_splits_into_a_bank_and_an_account(
        string token, string bank, string account)
    {
        FlutterwaveSettlementProvider.SplitDestination(token)
            .Should().Be((bank, account));
    }

    [Theory]
    [InlineData("0690000031")]
    [InlineData(":0690000031")]
    [InlineData("044:")]
    [InlineData("")]
    [InlineData(null)]
    public void A_token_that_is_not_bank_and_account_is_refused(string? token)
    {
        FlutterwaveSettlementProvider.SplitDestination(token)
            .Should().Be(((string?)null, (string?)null));
    }

    /// <summary>
    /// <b>Flutterwave answers 200 with <c>status: "error"</c>, so the HTTP code decides nothing.</b>
    /// An adapter trusting <c>IsSuccessStatusCode</c> would mark a withdrawal complete against money
    /// that never left. This is the single most important assertion in this file.
    /// </summary>
    [Fact]
    public void A_two_hundred_carrying_an_error_is_a_refusal_not_a_payment()
    {
        var outcome = FlutterwaveSettlementProvider.Read(
            """{"status":"error","message":"Insufficient funds in balance"}""",
            httpSucceeded: true,
            statusCode: 200);

        outcome.Disposition.Should().Be(SettlementDisposition.Refused);
        outcome.MayReleaseHold.Should().BeTrue();
        outcome.Message.Should().Contain("Insufficient funds");
    }

    /// <summary>
    /// A transfer Flutterwave has accepted settles asynchronously, so <c>NEW</c> and <c>PENDING</c>
    /// are acceptance: the instruction is with the provider and the money is committed.
    /// </summary>
    [Theory]
    [InlineData("NEW")]
    [InlineData("PENDING")]
    [InlineData("SUCCESSFUL")]
    public void An_accepted_transfer_is_accepted_whatever_stage_it_has_reached(string status)
    {
        var outcome = FlutterwaveSettlementProvider.Read(
            Success(status), httpSucceeded: true, statusCode: 200);

        outcome.IsAccepted.Should().BeTrue();
        outcome.ProviderReference.Should().Be("12345");
    }

    [Fact]
    public void A_failed_transfer_is_a_refusal()
    {
        var outcome = FlutterwaveSettlementProvider.Read(
            Success("FAILED"), httpSucceeded: true, statusCode: 200);

        outcome.Disposition.Should().Be(SettlementDisposition.Refused);
        outcome.MayReleaseHold.Should().BeTrue();
    }

    /// <summary>
    /// <b>A status nobody has seen before is neither paid nor refused.</b> Flutterwave adding a value
    /// must not make this adapter start releasing holds or completing payouts on it.
    /// </summary>
    [Fact]
    public void An_unrecognised_transfer_status_holds_the_money()
    {
        var outcome = FlutterwaveSettlementProvider.Read(
            Success("SOME_NEW_STATE"), httpSucceeded: true, statusCode: 200);

        outcome.Disposition.Should().Be(SettlementDisposition.Unknown);
        outcome.MayReleaseHold.Should().BeFalse();
    }

    [Fact]
    public void An_unreadable_body_holds_the_money()
    {
        var outcome = FlutterwaveSettlementProvider.Read(
            "<html>502</html>", httpSucceeded: true, statusCode: 200);

        outcome.Disposition.Should().Be(SettlementDisposition.Unknown);
        outcome.MayReleaseHold.Should().BeFalse();
    }

    // ── The status lookup, which is why this provider can resolve and PayPal cannot ──────────

    [Fact]
    public void A_transfer_found_by_reference_resolves_the_withdrawal()
    {
        var outcome = FlutterwaveSettlementStatusProvider.Read(
            """{"status":"success","data":[{"id":12345,"status":"SUCCESSFUL"}]}""", "abc123");

        outcome.IsAccepted.Should().BeTrue();
        outcome.ProviderReference.Should().Be("12345");
    }

    /// <summary>
    /// An empty list is Flutterwave positively holding no transfer for this reference, so nothing
    /// left the platform and the hold may go back. The sweep only asks after the grace window, which
    /// is what makes that safe to say.
    /// </summary>
    [Fact]
    public void No_transfer_for_the_reference_releases_the_hold()
    {
        var outcome = FlutterwaveSettlementStatusProvider.Read(
            """{"status":"success","data":[]}""", "abc123");

        outcome.Disposition.Should().Be(SettlementDisposition.Refused);
        outcome.MayReleaseHold.Should().BeTrue();
    }

    /// <summary>
    /// <b>A rejected lookup must never read as "no transfer exists".</b> A wrong key answering that
    /// way would release every held payout on the deployment.
    /// </summary>
    [Theory]
    [InlineData("""{"status":"error","message":"Authorization required"}""")]
    [InlineData("""{"status":"success"}""")]
    [InlineData("nonsense")]
    public void A_lookup_that_did_not_answer_leaves_the_money_held(string payload)
    {
        var outcome = FlutterwaveSettlementStatusProvider.Read(payload, "abc123");

        outcome.Disposition.Should().Be(SettlementDisposition.Unknown);
        outcome.MayReleaseHold.Should().BeFalse();
    }

    // Concatenated rather than interpolated: the JSON's own closing braces collide with raw-string
    // interpolation, and escaping them reads worse than escaping the quotes.
    private static string Success(string transferStatus) =>
        "{\"status\":\"success\",\"message\":\"ok\","
        + "\"data\":{\"id\":12345,\"status\":\"" + transferStatus + "\"}}";

    private static SettlementInstruction Instruction() =>
        new()
        {
            CounterpartyId = Guid.NewGuid(),
            Amount = Money.FromMinorUnits(1_000, "NGN"),
            PeriodStart = DateTimeOffset.UnixEpoch,
            PeriodEnd = DateTimeOffset.UnixEpoch,
            Reference = "abc123",
            DestinationReference = "044:0690000031",
            DestinationProvider = "flutterwave",
            DestinationKind = PayoutDestinationKind.BankAccount,
            DestinationCountry = "NG"
        };
}
