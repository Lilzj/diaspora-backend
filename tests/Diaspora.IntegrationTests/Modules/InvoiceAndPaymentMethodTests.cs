using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Financial;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Accounts;
using Diaspora.Modules.Financial.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Invoices and the saved-payment-method register, over real HTTP.
/// <para>
/// <b>Every invoice here is issued from a real captured payment</b> — a catalogue purchase, a
/// subscription charge or a direct payment, created, authorised and captured through the certified
/// routes with the simulated gateway. Nothing stages a captured payment directly.
/// </para>
/// <para>
/// The collection shares one database and runs its classes one at a time, so sequential numbers are
/// asserted relative to each other rather than as absolute values.
/// </para>
/// </summary>
[Collection(FinanceCollection.Name)]
public sealed partial class InvoiceAndPaymentMethodTests : PooledIdentityTestBase
{
    private const string Financial = "/api/v1/financial";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public InvoiceAndPaymentMethodTests(CustomWebApplicationFactory factory) : base(factory) =>
        factory.EnsureDatabaseCreated();

    // ── Invoices ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_member_who_has_paid_nothing_has_no_invoices()
    {
        await MemberAsync();

        var response = await HttpClient.GetAsync(new Uri($"{Financial}/invoices/me", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDataAsync<List<InvoiceShape>>(response)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_captured_purchase_is_invoiced_once_with_a_stable_number_and_no_invented_tax()
    {
        await MemberAsync();

        var paymentId = await CapturedPurchaseAsync("credit.bundle.25");

        var first = await InvoicesAsync();
        var invoice = first.Should().ContainSingle().Subject;

        NumberPattern().IsMatch(invoice.Number).Should().BeTrue(invoice.Number);
        invoice.Number.Should().StartWith(
            $"DSP-{DateTimeOffset.UtcNow.Year.ToString(CultureInfo.InvariantCulture)}-");
        invoice.Status.Should().Be("Paid");
        invoice.Kind.Should().Be("Purchase");
        invoice.PaymentIntentId.Should().Be(paymentId);

        var line = invoice.Lines.Should().ContainSingle().Subject;
        line.Description.Should().Be("£25 credit bundle");
        line.Quantity.Should().Be(1);
        line.UnitPrice.MinorUnits.Should().Be(2500);
        line.Total.MinorUnits.Should().Be(2500);
        line.SubjectType.Should().Be("Member");

        invoice.Subtotal.MinorUnits.Should().Be(2500);
        invoice.Total.MinorUnits.Should().Be(2500);
        invoice.Total.Currency.Should().Be("GBP");
        invoice.Tax.MinorUnits.Should().Be(0, "no record the invoice is drawn from carries tax");
        invoice.TaxLabel.Should().BeNull();
        invoice.TaxLines.Should().BeEmpty();
        invoice.Refunded.MinorUnits.Should().Be(0);
        invoice.PaidAt.Should().NotBeNull();
        invoice.DocumentUrl.Should().Be($"{Financial}/invoices/{invoice.InvoiceId}/document");

        // Reading again issues nothing new and renumbers nothing.
        var second = await InvoicesAsync();
        second.Should().ContainSingle().Which.Number.Should().Be(invoice.Number);

        var single = await ReadDataAsync<InvoiceShape>(await HttpClient.GetAsync(
            new Uri($"{Financial}/invoices/{invoice.InvoiceId}", UriKind.Relative)));

        single.Number.Should().Be(invoice.Number);
        single.Total.MinorUnits.Should().Be(2500);
    }

    [Fact]
    public async Task Numbers_are_sequential_across_the_platform_in_order_of_capture()
    {
        await MemberAsync();

        await CapturedPurchaseAsync("credit.bundle.25");
        await CapturedPurchaseAsync("credit.bundle.100");

        var invoices = await InvoicesAsync();

        invoices.Should().HaveCount(2);

        // Newest number first.
        var (newer, older) = (Sequence(invoices[0].Number), Sequence(invoices[1].Number));
        newer.Should().Be(older + 1);
        invoices[1].Lines.Single().Description.Should().Be("£25 credit bundle");
        invoices[0].Lines.Single().Description.Should().Be("£100 credit bundle");

        // A different member's next invoice continues the platform's sequence, not their own.
        await MemberAsync();
        await CapturedPurchaseAsync("credit.bundle.25");

        var theirs = (await InvoicesAsync()).Should().ContainSingle().Subject;
        Sequence(theirs.Number).Should().BeGreaterThan(newer);

        // The cursor pages strictly below a number already served.
        await MemberAsync();
        await CapturedPurchaseAsync("credit.bundle.25");
        await CapturedPurchaseAsync("credit.bundle.25");

        var all = await InvoicesAsync();
        var page = await ReadDataAsync<List<InvoiceShape>>(await HttpClient.GetAsync(new Uri(
            $"{Financial}/invoices/me?take=5&before={Uri.EscapeDataString(all[0].Number)}",
            UriKind.Relative)));

        page.Should().ContainSingle().Which.Number.Should().Be(all[1].Number);
    }

    [Fact]
    public async Task A_subscription_charge_is_invoiced_with_its_period()
    {
        await MemberAsync();

        var subscription = await ReadDataAsync<SubscriptionShape>(
            await HttpClient.PostAsJsonAsync(
                new Uri($"{Financial}/subscriptions", UriKind.Relative),
                new { planCode = "premium.monthly", withTrial = false }));

        var payments = await ReadDataAsync<List<PaymentShape>>(await HttpClient.GetAsync(
            new Uri($"{Financial}/payment-intents?take=5", UriKind.Relative)));

        var payment = payments.Should().ContainSingle().Subject;
        await CaptureAsync(payment.PaymentIntentId);

        var invoice = (await InvoicesAsync()).Should().ContainSingle().Subject;

        invoice.Kind.Should().Be("Subscription");
        invoice.PaymentIntentId.Should().Be(payment.PaymentIntentId);
        invoice.PeriodStart.Should().BeCloseTo(subscription.CurrentPeriodStart, TimeSpan.FromSeconds(1));
        invoice.PeriodEnd.Should().BeAfter(invoice.PeriodStart, "a monthly plan's period has an end");
        invoice.Tax.MinorUnits.Should().Be(0);
    }

    [Fact]
    public async Task Someone_elses_invoice_and_its_document_are_not_found()
    {
        await MemberAsync();
        await CapturedPurchaseAsync("credit.bundle.25");

        var invoice = (await InvoicesAsync()).Single();

        await MemberAsync();

        var read = await HttpClient.GetAsync(
            new Uri($"{Financial}/invoices/{invoice.InvoiceId}", UriKind.Relative));
        var document = await HttpClient.GetAsync(
            new Uri($"{Financial}/invoices/{invoice.InvoiceId}/document", UriKind.Relative));

        read.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CodeAsync(read)).Should().Be(FinancialErrorCodes.InvoiceNotFound);
        document.StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await InvoicesAsync()).Should().BeEmpty("the other member's invoice is not in this list");
    }

    [Fact]
    public async Task Invoices_are_not_readable_without_a_token()
    {
        HttpClient.DefaultRequestHeaders.Authorization = null;

        var response = await HttpClient.GetAsync(new Uri($"{Financial}/invoices/me", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// <b>A direct payment's description is whatever the payer typed.</b> The document is served from
    /// the API's origin, so it is encoded and the page is forbidden from running anything.
    /// </summary>
    [Fact]
    public async Task The_document_is_html_for_the_owner_with_every_value_encoded()
    {
        await MemberAsync();

        var intent = await ReadDataAsync<PaymentShape>(
            await HttpClient.PostAsJsonAsync(
                new Uri($"{Financial}/payment-intents", UriKind.Relative),
                new
                {
                    amount = 12.5m,
                    currency = "GBP",
                    methodKind = "Card",
                    description = "<script>alert(1)</script> Hall hire",
                    idempotencyKey = Guid.NewGuid().ToString("N")
                }));

        await CaptureAsync(intent.PaymentIntentId);

        var invoice = (await InvoicesAsync()).Should().ContainSingle().Subject;
        invoice.Kind.Should().Be("Payment");
        invoice.Lines.Single().Description.Should().Be("<script>alert(1)</script> Hall hire");

        var response = await HttpClient.GetAsync(new Uri(invoice.DocumentUrl, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        response.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("default-src 'none'");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();

        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain(invoice.Number);
        html.Should().Contain("&lt;script&gt;alert(1)&lt;/script&gt; Hall hire");
        html.Should().NotContain("<script>");
        // The encoder writes every non-ASCII character as a numeric reference: "&#163;" is "£".
        System.Net.WebUtility.HtmlDecode(html).Should().Contain("£12.50");
        html.Should().Contain("&#163;12.50");
    }

    // ── Saved payment methods ────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Nothing reusable is stored, so the honest list is empty</b> — even for a member who has
    /// paid by card, because the card was entered on the provider's page for that payment alone.
    /// </summary>
    [Fact]
    public async Task The_platform_keeps_no_payment_methods_even_after_a_card_payment()
    {
        await MemberAsync();
        await CapturedPurchaseAsync("credit.bundle.25");

        var response = await HttpClient.GetAsync(
            new Uri($"{Financial}/payment-methods/me", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDataAsync<List<JsonElement>>(response)).Should().BeEmpty();
    }

    [Fact]
    public async Task Forgetting_a_payment_method_finds_nothing_to_forget()
    {
        await MemberAsync();

        var response = await HttpClient.DeleteAsync(
            new Uri($"{Financial}/payment-methods/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CodeAsync(response)).Should().Be(FinancialErrorCodes.PaymentMethodNotFound);
    }

    // ── Arrangement ──────────────────────────────────────────────────────────────────────────

    /// <summary>A verified member whose financial identity is active, signed in on the client.</summary>
    private async Task MemberAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
        HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Tokens.AccessToken);

        await Factory.DrainOutboxAsync();

        using var scope = Factory.Services.CreateScope();

        var accounts = scope.ServiceProvider.GetRequiredService<IFinancialAccountRepository>();
        var identities = scope.ServiceProvider.GetRequiredService<FinancialIdentityService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var account = await accounts.FindByOwnerAsync(auth.User.Id, FinancialAccountType.Personal)
            ?? await identities.GetOrCreateAsync(auth.User.Id, FinancialAccountType.Personal, "GBP");

        await identities.ActivateAsync(account);
        await unitOfWork.SaveChangesAsync();
    }

    /// <summary>Buys a product through the real route and captures its payment. Returns the payment.</summary>
    private async Task<Guid> CapturedPurchaseAsync(string productCode)
    {
        var response = await HttpClient.PostAsJsonAsync(
            new Uri($"{Financial}/purchases", UriKind.Relative),
            new { productCode, methodKind = "Card", idempotencyKey = Guid.NewGuid().ToString("N") });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var purchase = await ReadDataAsync<PurchaseShape>(response);
        var paymentId = purchase.PaymentIntentId
            ?? throw new InvalidOperationException("A priced product opens a payment.");

        await CaptureAsync(paymentId);

        return paymentId;
    }

    private async Task CaptureAsync(Guid paymentIntentId)
    {
        var authorized = await HttpClient.PostAsJsonAsync(
            new Uri($"{Financial}/payment-intents/{paymentIntentId}/authorize", UriKind.Relative),
            new { });

        authorized.StatusCode.Should().Be(HttpStatusCode.OK, await authorized.Content.ReadAsStringAsync());

        var captured = await HttpClient.PostAsJsonAsync(
            new Uri($"{Financial}/payment-intents/{paymentIntentId}/capture", UriKind.Relative),
            new { });

        captured.StatusCode.Should().Be(HttpStatusCode.OK, await captured.Content.ReadAsStringAsync());
    }

    private async Task<List<InvoiceShape>> InvoicesAsync()
    {
        var response = await HttpClient.GetAsync(new Uri($"{Financial}/invoices/me", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return await ReadDataAsync<List<InvoiceShape>>(response);
    }

    private static long Sequence(string number) =>
        long.Parse(number.Split('-')[2], CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^DSP-\d{4}-\d{6,}$")]
    private static partial Regex NumberPattern();

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

    private sealed record MoneyShape(decimal Amount, string Currency, long MinorUnits, int Exponent);

    private sealed record LineShape(
        string Description,
        int Quantity,
        MoneyShape UnitPrice,
        MoneyShape Total,
        string? SubjectType,
        Guid? SubjectId);

    private sealed record InvoiceShape(
        Guid InvoiceId,
        string Number,
        string Status,
        string Kind,
        DateTimeOffset PeriodStart,
        DateTimeOffset PeriodEnd,
        List<LineShape> Lines,
        MoneyShape Subtotal,
        MoneyShape Tax,
        string? TaxLabel,
        List<JsonElement> TaxLines,
        MoneyShape Total,
        MoneyShape Refunded,
        DateTimeOffset IssuedAt,
        DateTimeOffset? PaidAt,
        Guid? PaymentIntentId,
        string DocumentUrl);

    private sealed record PurchaseShape(Guid PurchaseId, Guid? PaymentIntentId);

    private sealed record PaymentShape(Guid PaymentIntentId, string Status);

    private sealed record SubscriptionShape(Guid SubscriptionId, DateTimeOffset CurrentPeriodStart);
}
