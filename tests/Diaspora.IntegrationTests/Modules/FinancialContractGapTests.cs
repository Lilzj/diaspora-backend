using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Financial;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The eleven places where a capability existed and no HTTP surface reached it.
/// <para>
/// <b>Every test here is a route the frontend registered as a gap and worked around on screen.</b>
/// The workarounds were honest — one page with the ceiling stated, a transaction header without its
/// entries, a receipt screen that explained why it could not find an anonymous gift — and each of
/// them can now be deleted.
/// </para>
/// </summary>
[Collection(FinanceCollection.Name)]
public sealed class FinancialContractGapTests : PooledIdentityTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public FinancialContractGapTests(CustomWebApplicationFactory factory) : base(factory) =>
        factory.EnsureDatabaseCreated();

    // ── GAP-06 · the cursors that existed in the application layer and nowhere on the wire ───

    /// <summary>
    /// <b>`GetTransactionsQuery` supported all four of these before B15.1 and the route bound
    /// none.</b> The frontend's sharpest observation, and a two-line fix once somebody looked.
    /// </summary>
    [Fact]
    public async Task The_transaction_route_binds_the_cursor_the_query_always_supported()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(new Uri(
            "/api/v1/financial/wallets/me/transactions"
            + $"?from={Uri.EscapeDataString("2020-01-01T00:00:00Z")}"
            + $"&to={Uri.EscapeDataString("2030-01-01T00:00:00Z")}"
            + $"&before={Uri.EscapeDataString("2030-01-01T00:00:00Z")}"
            + $"&excludeIds={Guid.NewGuid()}&excludeIds={Guid.NewGuid()}"
            + "&pageSize=5",
            UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/api/v1/financial/payment-intents")]
    [InlineData("/api/v1/financial/purchases")]
    [InlineData("/api/v1/financial/placements/me")]
    public async Task The_four_other_list_routes_take_a_cursor(string route)
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(new Uri(
            $"{route}?before={Uri.EscapeDataString("2030-01-01T00:00:00Z")}"
            + $"&excludeIds={Guid.NewGuid()}&take=5",
            UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Giving_history_takes_a_cursor()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(new Uri(
            "/api/v1/financial/giving/me"
            + $"?before={Uri.EscapeDataString("2030-01-01T00:00:00Z")}&take=5",
            UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── GAP-07, 08, 09 · resolving one thing by its identifier ──────────────────────────────

    /// <summary>
    /// Answers 404 rather than 403 on a transaction with no leg of the caller's, so an identifier
    /// cannot be used to discover that one exists.
    /// </summary>
    [Fact]
    public async Task A_transaction_that_is_not_the_callers_is_not_found()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(new Uri(
            $"/api/v1/financial/transactions/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CodeAsync(response)).Should().Be(FinancialErrorCodes.TransactionNotFound);
    }

    [Fact]
    public async Task A_placement_that_is_not_the_callers_is_not_found()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(new Uri(
            $"/api/v1/financial/placements/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CodeAsync(response)).Should().Be(FinancialErrorCodes.PlacementNotFound);
    }

    /// <summary>
    /// The route the notification for an anonymous donation deep-links to. It resolves by reference
    /// alone; an unknown reference is a 404 rather than a 403, because there is no owner to compare.
    /// </summary>
    [Fact]
    public async Task A_receipt_resolves_by_reference_and_an_unknown_one_is_not_found()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(new Uri(
            "/api/v1/financial/receipts/DIA-NOT-A-REAL-REFERENCE", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// A `{guid}` in the first segment resolves a placement rather than being read as a subject
    /// type — the route ordering that would otherwise silently shadow the by-id route.
    /// </summary>
    [Fact]
    public async Task The_placement_by_id_route_does_not_shadow_the_by_subject_route()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(new Uri(
            $"/api/v1/financial/placements/MarketplaceListing/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── MON-GAP-008 · entitlements ──────────────────────────────────────────────────────────

    /// <summary>
    /// <b>A member on no plan gets an empty snapshot, not a 404.</b> "You are entitled to nothing"
    /// is the answer; "that does not exist" is a different and wrong one.
    /// </summary>
    [Fact]
    public async Task The_entitlement_snapshot_answers_for_a_member_on_no_plan()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(new Uri(
            "/api/v1/financial/entitlements/me", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var snapshot = await ReadDataAsync<SnapshotShape>(response);

        snapshot.Entitlements.Should().BeEmpty();
        snapshot.Counters.Should().BeEmpty();
        snapshot.SourcePlanCode.Should().BeNull();
    }

    // ── MON-GAP-003 · plan change ───────────────────────────────────────────────────────────

    /// <summary>404 on somebody else's subscription, and on one that does not exist.</summary>
    [Fact]
    public async Task A_plan_change_preview_for_a_subscription_that_is_not_the_callers_is_not_found()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(new Uri(
            $"/api/v1/financial/subscriptions/{Guid.NewGuid()}/change/preview?planCode=premium.monthly",
            UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CodeAsync(response)).Should().Be(FinancialErrorCodes.SubscriptionNotFound);
    }

    [Fact]
    public async Task The_upcoming_charge_route_answers_empty_for_a_member_with_no_subscription()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(new Uri(
            "/api/v1/financial/subscriptions/me/upcoming", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var charges = await ReadDataAsync<List<JsonElement>>(response);

        charges.Should().BeEmpty();
    }

    // ── GAP-05 · refund requests ────────────────────────────────────────────────────────────

    /// <summary>
    /// A refund request for a payment that is not the caller's is a 404, never a 403 — a 403 would
    /// confirm the payment exists.
    /// </summary>
    [Fact]
    public async Task A_refund_request_against_somebody_elses_payment_is_not_found()
    {
        // Verified, because asking for a refund opens at that tier — the tier itself is asserted by
        // Nothing_b15_1_added_that_moves_money_is_reachable_below_the_verified_tier, and this test is
        // about what a member who *may* ask is told when they ask about a payment that is not theirs.
        await AuthenticateVerifiedAsync();

        var response = await HttpClient.PostAsJsonAsync(
            new Uri(
                $"/api/v1/financial/payment-intents/{Guid.NewGuid()}/refund-requests",
                UriKind.Relative),
            new { reason = "RequestedByPayer", note = "It never arrived." });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CodeAsync(response)).Should().Be(FinancialErrorCodes.PaymentIntentNotFound);
    }

    /// <summary>
    /// <b>Deciding a refund is staff-only, and stays that way.</b> B15's refusal was correct and this
    /// phase did not widen it — it added a way to *ask*, which is a different thing.
    /// </summary>
    [Fact]
    public async Task A_member_cannot_decide_a_refund_request()
    {
        await AuthenticateAsync();

        var refused = new[]
        {
            await HttpClient.GetAsync(new Uri(
                "/api/v1/financial/operations/refund-requests", UriKind.Relative)),
            await HttpClient.PostAsJsonAsync(
                new Uri(
                    $"/api/v1/financial/operations/refund-requests/{Guid.NewGuid()}/decide",
                    UriKind.Relative),
                new { approve = true })
        };

        refused.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// <b>Every money-moving surface B15.1 added opens at Verified and nowhere earlier</b>, and every
    /// read a member holds from the membership rung still answers. The two halves together are the
    /// tier decision, asserted so a later widening fails a test rather than a review.
    /// </summary>
    [Fact]
    public async Task Nothing_b15_1_added_that_moves_money_is_reachable_below_the_verified_tier()
    {
        await AuthenticateAsync();

        var refused = new[]
        {
            await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/wallets/me/deposits", UriKind.Relative),
                new { amount = 25m, currency = "GBP", methodKind = "Card" }),
            await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/wallets/me/withdrawals", UriKind.Relative),
                new { amount = 25m, currency = "GBP", destinationId = Guid.NewGuid() }),
            await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/rewards/conversions", UriKind.Relative),
                new { points = 100L, currency = "GBP" }),
            await HttpClient.PostAsJsonAsync(
                new Uri("/api/v1/financial/payout-destinations", UriKind.Relative),
                new { kind = "BankAccount", label = "Test ••••1111", currency = "GBP" }),
            await HttpClient.PostAsJsonAsync(
                new Uri($"/api/v1/financial/placements/{Guid.NewGuid()}/renew", UriKind.Relative),
                new { })
        };

        refused.Should().OnlyContain(
            response => response.StatusCode == HttpStatusCode.Forbidden,
            "depositing, withdrawing, converting, registering a destination and renewing all move "
            + "money or commit to moving it");

        var readable = new[]
        {
            await HttpClient.GetAsync(new Uri(
                "/api/v1/financial/rewards/me", UriKind.Relative)),
            await HttpClient.GetAsync(new Uri(
                "/api/v1/financial/entitlements/me", UriKind.Relative)),
            await HttpClient.GetAsync(new Uri(
                "/api/v1/financial/wallets/me/analytics", UriKind.Relative)),
            await HttpClient.GetAsync(new Uri(
                "/api/v1/financial/wallets/me/deposits/limits", UriKind.Relative))
        };

        readable.Should().OnlyContain(
            response => response.StatusCode == HttpStatusCode.OK,
            "reading what you hold survives the tier and survives restriction");
    }

    // ── PHASE15-GAP-018 · wallet analytics ──────────────────────────────────────────────────

    /// <summary>
    /// Summed server-side, grouped by currency, and refusing an inverted range rather than silently
    /// swapping the dates.
    /// </summary>
    [Fact]
    public async Task Wallet_analytics_refuses_an_inverted_period()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(new Uri(
            "/api/v1/financial/wallets/me/analytics"
            + $"?from={Uri.EscapeDataString("2030-01-01T00:00:00Z")}"
            + $"&to={Uri.EscapeDataString("2020-01-01T00:00:00Z")}",
            UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Wallet_analytics_never_totals_across_currencies()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(new Uri(
            "/api/v1/financial/wallets/me/analytics", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var data = payload.GetProperty("data");

        // Every figure is inside a row that names its currency. A bare "total" would need an
        // exchange rate the platform has no authority to pick.
        data.TryGetProperty("total", out _).Should().BeFalse();
        data.TryGetProperty("grandTotal", out _).Should().BeFalse();
        data.GetProperty("totals").ValueKind.Should().Be(JsonValueKind.Array);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────

    private async Task AuthenticateAsync()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        Api.Authenticate(auth.Tokens.AccessToken);

        HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Tokens.AccessToken);
    }

    private async Task AuthenticateVerifiedAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        Api.Authenticate(auth.Tokens.AccessToken);

        HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Tokens.AccessToken);
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        return problem.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        return payload.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }

    private sealed record SnapshotShape(
        List<JsonElement> Entitlements, List<JsonElement> Counters, string? SourcePlanCode);
}
