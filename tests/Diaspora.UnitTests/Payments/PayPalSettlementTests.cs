using System.Net;
using System.Text.Json;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Diaspora.Payments.PayPal;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Payments;

/// <summary>
/// The request the PayPal adapter builds, and what it makes of the answer.
/// <para>
/// <b>These pin a payload that has never been executed against PayPal.</b> The adapter was written
/// from the REST payouts API without a sandbox account to try it on, so what is under test is that
/// the adapter builds the request it means to and classifies replies safely — not that PayPal
/// accepts it. Every capability row for this provider stays <c>Unknown</c> until somebody runs the
/// live sandbox test, and these tests exist so that a correction to the payload is a visible diff.
/// </para>
/// </summary>
public class PayPalSettlementTests
{
    [Fact]
    public void A_payout_is_a_one_item_batch_carrying_the_receiver_and_the_amount()
    {
        var body = PayPalSettlementProvider.BuildPayout(Instruction(), "diaspora-payout-abc123");

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        root.GetProperty("sender_batch_header").GetProperty("sender_batch_id").GetString()
            .Should().Be("diaspora-payout-abc123");

        var items = root.GetProperty("items");

        items.GetArrayLength().Should().Be(1,
            "one member's money per call, so a partial batch failure cannot happen");

        var item = items[0];

        item.GetProperty("receiver").GetString().Should().Be("payee@example.com");
        item.GetProperty("amount").GetProperty("value").GetString().Should().Be("10.00");
        item.GetProperty("amount").GetProperty("currency").GetString().Should().Be("GBP");
    }

    /// <summary>
    /// An email address and a payer id are different recipient types and PayPal rejects the wrong
    /// one. The '@' is the only thing that distinguishes them without asking.
    /// </summary>
    [Theory]
    [InlineData("payee@example.com", "EMAIL")]
    [InlineData("A4BCD1234EFGH", "PAYPAL_ID")]
    public void The_recipient_type_follows_the_shape_of_the_receiver(string receiver, string expected)
    {
        var body = PayPalSettlementProvider.BuildPayout(
            Instruction() with { DestinationReference = receiver }, "batch");

        using var document = JsonDocument.Parse(body);

        document.RootElement.GetProperty("items")[0]
            .GetProperty("recipient_type").GetString().Should().Be(expected);
    }

    /// <summary>
    /// PayPal takes a decimal string rather than minor units, and the exponent comes off the amount
    /// so a zero-decimal currency is not sent with a spurious ".00".
    /// </summary>
    [Fact]
    public void A_zero_decimal_currency_carries_no_decimal_places()
    {
        var body = PayPalSettlementProvider.BuildPayout(
            Instruction() with { Amount = Money.FromMinorUnits(2_500, "JPY", exponent: 0) }, "batch");

        using var document = JsonDocument.Parse(body);
        var amount = document.RootElement.GetProperty("items")[0].GetProperty("amount");

        amount.GetProperty("value").GetString().Should().Be("2500");
        amount.GetProperty("currency").GetString().Should().Be("JPY");
    }

    [Fact]
    public void The_batch_id_is_the_withdrawals_own_identifier_so_a_repeat_replays()
    {
        var instruction = Instruction();

        PayPalSettlementProvider.SenderBatchId(instruction)
            .Should().Be("diaspora-payout-abc123");

        PayPalSettlementProvider.SenderBatchId(instruction)
            .Should().Be(PayPalSettlementProvider.SenderBatchId(instruction),
                "a key that changed between attempts would make every retry a second payout");
    }

    /// <summary>
    /// <b>A batch PayPal has already seen is one it already acted on.</b> Reading that conflict as a
    /// refusal would release a hold on money that has gone — it is a success arriving as an error.
    /// </summary>
    [Fact]
    public void An_already_submitted_batch_is_recognised_as_accepted_rather_than_refused()
    {
        const string Payload =
            """{"name":"BATCH_ID_ALREADY_EXISTS","message":"A batch with this id already exists."}""";

        PayPalSettlementProvider.IsDuplicateBatch(HttpStatusCode.BadRequest, Payload)
            .Should().BeTrue();

        PayPalSettlementProvider.IsDuplicateBatch(
            HttpStatusCode.BadRequest, """{"name":"VALIDATION_ERROR","message":"nope"}""")
            .Should().BeFalse("an ordinary validation failure is a genuine refusal");
    }

    [Fact]
    public void The_batch_id_is_read_back_off_an_accepted_payout()
    {
        PayPalSettlementProvider.ReadBatchId("""{"batch_header":{"payout_batch_id":"PB123"}}""")
            .Should().Be("PB123");

        PayPalSettlementProvider.ReadBatchId("not json").Should().BeNull();
    }

    [Fact]
    public void An_error_is_described_by_paypals_own_name_and_message()
    {
        PayPalSettlementProvider.ReadError(
            """{"name":"INSUFFICIENT_FUNDS","message":"Not enough balance."}""")
            .Should().Contain("INSUFFICIENT_FUNDS").And.Contain("Not enough balance.");

        PayPalSettlementProvider.ReadError("<html>").Should().Be("unparseable error body");
    }

    private static SettlementInstruction Instruction() =>
        new()
        {
            CounterpartyId = Guid.NewGuid(),
            Amount = Money.FromMinorUnits(1_000, "GBP"),
            PeriodStart = DateTimeOffset.UnixEpoch,
            PeriodEnd = DateTimeOffset.UnixEpoch,
            Reference = "abc123",
            DestinationReference = "payee@example.com",
            DestinationProvider = "paypal",
            DestinationKind = PayoutDestinationKind.ProviderManaged,
            DestinationCountry = "GB"
        };
}
