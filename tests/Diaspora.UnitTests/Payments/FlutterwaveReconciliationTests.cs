using System.Text.Json;
using Diaspora.Payments.Flutterwave;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Payments;

/// <summary>
/// Reading Flutterwave's own record of what it paid.
/// <para>
/// <b>The rows this produces are the only way an orphan payout is ever found</b> — a transfer the
/// provider made that no withdrawal accounts for. So the failure that matters here is not a crash;
/// it is dropping a row silently, because a reconciliation pass that examines nothing reports a
/// clean bill of health.
/// </para>
/// </summary>
public class FlutterwaveReconciliationTests
{
    private static readonly DateTimeOffset From = new(2026, 8, 28, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 8, 31, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_settled_transfer_inside_the_window_is_reported()
    {
        var (records, _) = FlutterwavePayoutReconciliationProvider.ReadPage(
            "flutterwave", Page(Row("2026-08-30T10:00:00Z", "SUCCESSFUL")), From, To);

        var record = records.Should().ContainSingle().Subject;

        record.Status.Should().Be("paid");
        record.ProviderReference.Should().Be("12345");
        record.Reference.Should().Be("diaspora-payout-abc123");
        record.Currency.Should().Be("NGN");
        record.AmountMinorUnits.Should().Be(1_000, "10.00 in minor units");
    }

    [Theory]
    [InlineData("SUCCESSFUL", "paid")]
    [InlineData("FAILED", "failed")]
    [InlineData("REVERSED", "reversed")]
    public void Provider_statuses_normalise_to_the_platforms_three(string status, string expected)
    {
        var (records, _) = FlutterwavePayoutReconciliationProvider.ReadPage(
            "flutterwave", Page(Row("2026-08-30T10:00:00Z", status)), From, To);

        records.Should().ContainSingle().Which.Status.Should().Be(expected);
    }

    /// <summary>
    /// <b>A transfer still moving is not a discrepancy.</b> Reporting one would have the sweep flag
    /// every in-flight payout as a mismatch, every time it ran.
    /// </summary>
    [Theory]
    [InlineData("NEW")]
    [InlineData("PENDING")]
    public void A_transfer_still_in_flight_is_left_out(string status)
    {
        var (records, _) = FlutterwavePayoutReconciliationProvider.ReadPage(
            "flutterwave", Page(Row("2026-08-30T10:00:00Z", status)), From, To);

        records.Should().BeEmpty();
    }

    [Fact]
    public void Transfers_outside_the_window_are_left_out()
    {
        var (records, _) = FlutterwavePayoutReconciliationProvider.ReadPage(
            "flutterwave",
            Page(
                Row("2026-08-01T10:00:00Z", "SUCCESSFUL"),
                Row("2026-08-30T10:00:00Z", "SUCCESSFUL"),
                Row("2026-09-15T10:00:00Z", "SUCCESSFUL")),
            From,
            To);

        records.Should().ContainSingle("only one of the three is inside the window");
    }

    /// <summary>
    /// Flutterwave returns transfers newest first, so a page entirely older than the window means
    /// every later page is too. Stopping there is the difference between three requests and fifty.
    /// </summary>
    [Fact]
    public void A_page_entirely_older_than_the_window_ends_the_paging()
    {
        var (_, exhausted) = FlutterwavePayoutReconciliationProvider.ReadPage(
            "flutterwave",
            Page(Row("2026-07-01T10:00:00Z", "SUCCESSFUL"), Row("2026-07-02T10:00:00Z", "SUCCESSFUL")),
            From,
            To);

        exhausted.Should().BeTrue();
    }

    [Fact]
    public void A_page_containing_the_window_does_not_end_the_paging()
    {
        var (_, exhausted) = FlutterwavePayoutReconciliationProvider.ReadPage(
            "flutterwave", Page(Row("2026-08-30T10:00:00Z", "SUCCESSFUL")), From, To);

        exhausted.Should().BeFalse();
    }

    [Fact]
    public void An_empty_page_ends_the_paging()
    {
        var (records, exhausted) = FlutterwavePayoutReconciliationProvider.ReadPage(
            "flutterwave", """{"status":"success","data":[]}""", From, To);

        records.Should().BeEmpty();
        exhausted.Should().BeTrue();
    }

    /// <summary>
    /// <b>A row this adapter cannot date must not be the reason it stops reading.</b> Treating an
    /// undated row as older than the window would end the pass early and leave the rest of it
    /// unexamined — the silent half-check this whole component exists to avoid.
    /// </summary>
    [Fact]
    public void A_row_with_no_usable_date_does_not_end_the_paging()
    {
        const string Body =
            """{"status":"success","data":[{"id":1,"status":"SUCCESSFUL","currency":"NGN","amount":10}]}""";

        var (records, exhausted) = FlutterwavePayoutReconciliationProvider.ReadPage(
            "flutterwave", Body, From, To);

        records.Should().BeEmpty("it cannot be placed in the window");
        exhausted.Should().BeFalse("but it must not stop the pass either");
    }

    [Fact]
    public void An_unreadable_body_ends_the_paging_rather_than_throwing()
    {
        var (records, exhausted) = FlutterwavePayoutReconciliationProvider.ReadPage(
            "flutterwave", "<html>", From, To);

        records.Should().BeEmpty();
        exhausted.Should().BeTrue();
    }

    /// <summary>
    /// A transfer with no currency cannot be compared against a withdrawal at all, so it is left out
    /// rather than reported with an invented one.
    /// </summary>
    [Fact]
    public void A_transfer_with_no_currency_is_left_out()
    {
        const string Body =
            """
            {"status":"success","data":[{"id":1,"status":"SUCCESSFUL","amount":10,
             "created_at":"2026-08-30T10:00:00Z"}]}
            """;

        var (records, _) = FlutterwavePayoutReconciliationProvider.ReadPage(
            "flutterwave", Body, From, To);

        records.Should().BeEmpty();
    }

    [Fact]
    public void A_string_amount_is_read_the_same_as_a_numeric_one()
    {
        using var document = JsonDocument.Parse(
            """{"id":1,"status":"SUCCESSFUL","currency":"NGN","amount":"10.00"}""");

        var record = FlutterwavePayoutReconciliationProvider.Map(
            "flutterwave", document.RootElement, To);

        record!.AmountMinorUnits.Should().Be(1_000);
    }

    private static string Page(params string[] rows) =>
        "{\"status\":\"success\",\"data\":[" + string.Join(",", rows) + "]}";

    private static string Row(string createdAt, string status) =>
        "{\"id\":12345,\"reference\":\"diaspora-payout-abc123\",\"currency\":\"NGN\","
        + "\"amount\":10.00,\"status\":\"" + status + "\",\"created_at\":\"" + createdAt + "\"}";
}
