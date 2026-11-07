using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Financial;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Accounts;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Domain.Wallets;
using Diaspora.Modules.Financial.Infrastructure.Gateways;
using Diaspora.Modules.Notifications.Domain.Notifications;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Staff decisions about members' money need a reason, a notice, a guard and an appeal (7 Nov 2026).
/// <para>
/// Over real HTTP, on the host that takes deposits, because the one refund that takes money back from
/// a member is a deposit's. Notices are read back from the member's inbox as rendered text, as
/// <c>ModerationNoticeTests</c> does: a notice whose template drops a value, or whose rule never
/// matches, reaches nobody, and only the rendered body shows that.
/// </para>
/// </summary>
public sealed class MoneyDueProcessTests : IdentityTestBase<FinancialMovementWebApplicationFactory>
{
    private const string Financial = "/api/v1/financial";

    private const string Currency = "GBP";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public MoneyDueProcessTests(FinancialMovementWebApplicationFactory factory) : base(factory)
    {
        factory.EnsureDatabaseCreated();
    }

    // ── Wallet freezes ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_freeze_without_a_reason_is_refused()
    {
        var (member, _) = await MemberAsync();
        var walletId = await PrimaryWalletIdAsync(member);

        await SignInAsRoleAsync("Admin");

        var none = await SetWalletStatusAsync(walletId, "Frozen", reason: null);
        none.StatusCode.Should().Be(HttpStatusCode.BadRequest, await none.Content.ReadAsStringAsync());
        (await CodeAsync(none)).Should().Be(FinancialErrorCodes.WalletStatusReasonRequired);

        var token = await SetWalletStatusAsync(walletId, "Frozen", reason: "fraud");
        token.StatusCode.Should().Be(HttpStatusCode.BadRequest, "a word is not a reason a member can act on");

        var close = await SetWalletStatusAsync(walletId, "Closed", reason: null);
        close.StatusCode.Should().Be(HttpStatusCode.BadRequest, "closing needs a reason as freezing does");

        (await WalletAsync(walletId)).Status.Should().Be(WalletStatus.Active, "nothing changed");
    }

    [Fact]
    public async Task A_freeze_tells_the_member_the_reason_in_the_notice_and_the_push_line()
    {
        var (member, memberAuth) = await MemberAsync();
        var walletId = await PrimaryWalletIdAsync(member);

        await SignInAsRoleAsync("Admin");
        (await SetWalletStatusAsync(walletId, "Frozen", "Two deposits were reversed by the card issuer."))
            .EnsureSuccessStatusCode();

        (await WalletAsync(walletId)).StatusReason.Should().Be("Two deposits were reversed by the card issuer.");

        await Factory.DrainOutboxAsync();

        ActAs(memberAuth);
        var frozen = (await BillingInboxAsync())
            .Should().ContainSingle(notice => notice.Title == "Your wallet is now frozen").Subject;

        frozen.Body.Should().Contain("Two deposits were reversed by the card issuer.");
        frozen.Body.Should().NotContain("..", "the member's sentence and the template's are joined once");
        frozen.Body.Should().Contain("Profile, Appeals", "and the member is told they can appeal");

        (await ShortBodiesAsync(member, "billing.wallet.status.v2"))
            .Should().ContainSingle().Which.Should().Contain("Two deposits were reversed by the card issuer",
                "the push line carries the reason too");
    }

    [Fact]
    public async Task An_upheld_money_appeal_unfreezes_the_wallet_with_the_reviewers_explanation()
    {
        var (member, memberAuth) = await MemberAsync();
        var walletId = await PrimaryWalletIdAsync(member);

        await SignInAsRoleAsync("Admin");
        (await SetWalletStatusAsync(walletId, "Frozen", "Suspected chargeback abuse on two deposits."))
            .EnsureSuccessStatusCode();

        ActAs(memberAuth);
        var appealId = await FileMoneyAppealAsync(walletId, "Both deposits were mine; the bank reversed them in error.");

        await SignInAsRoleAsync("Moderator");
        (await ReviewAsync(appealId, "Approve", "The issuer confirmed both deposits were genuine."))
            .EnsureSuccessStatusCode();

        await Factory.DrainOutboxAsync();
        await Factory.DrainOutboxAsync();

        var wallet = await WalletAsync(walletId);
        wallet.Status.Should().Be(WalletStatus.Active);
        wallet.StatusReason.Should().Be("The issuer confirmed both deposits were genuine.");

        // Told, within a minute of the freeze notice: the wallet rule no longer swallows the second.
        ActAs(memberAuth);
        var notices = await BillingInboxAsync();
        notices.Should().ContainSingle(notice => notice.Title == "Your wallet is now frozen");
        notices.Should().ContainSingle(notice => notice.Title == "Your wallet is now active")
            .Which.Body.Should().Contain("The issuer confirmed both deposits were genuine.");
    }

    [Fact]
    public async Task A_money_appeal_against_somebody_elses_wallet_changes_nothing()
    {
        var (owner, _) = await MemberAsync();
        var walletId = await PrimaryWalletIdAsync(owner);

        await SignInAsRoleAsync("Admin");
        (await SetWalletStatusAsync(walletId, "Frozen", "Suspected chargeback abuse on two deposits."))
            .EnsureSuccessStatusCode();

        var (_, strangerAuth) = await MemberAsync();
        ActAs(strangerAuth);
        var appealId = await FileMoneyAppealAsync(walletId, "Please unfreeze this wallet, it is not mine.");

        await SignInAsRoleAsync("Moderator");
        (await ReviewAsync(appealId, "Approve", "Approved without checking the wallet."))
            .EnsureSuccessStatusCode();

        await Factory.DrainOutboxAsync();
        await Factory.DrainOutboxAsync();

        (await WalletAsync(walletId)).Status.Should().Be(WalletStatus.Frozen,
            "Trust cannot see whose wallet it is, so Financial checks, and ignores an appeal that is not the owner's");
    }

    // ── Refunds ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A refund of a deposit takes the money back out of the depositor's own wallet. The depositor is
    /// the payee who loses the balance, and was not told until now.
    /// </summary>
    [Fact]
    public async Task The_payee_of_a_refund_is_told_the_amount_and_the_reason()
    {
        var (member, memberAuth) = await MemberAsync();

        var depositId = await CreateDepositAsync(25m);
        await ConfirmByWebhookAsync(depositId, 2500);
        (await AvailableAsync(member)).Should().Be(2500);

        await SignInAsRoleAsync("Admin");
        var refunded = await HttpClient.PostAsJsonAsync(
            new Uri($"{Financial}/payment-intents/{depositId}/refunds", UriKind.Relative),
            new { reason = "Duplicate", note = "The member deposited twice by mistake." });
        refunded.StatusCode.Should().Be(HttpStatusCode.OK, await refunded.Content.ReadAsStringAsync());

        (await AvailableAsync(member)).Should().Be(0, "the refund took the deposit back out of the wallet");

        await Factory.DrainOutboxAsync();

        ActAs(memberAuth);
        var notices = await BillingInboxAsync();

        var taken = notices.Should().ContainSingle(notice => notice.Title.Contains("was taken out of your wallet")).Subject;
        taken.Body.Should().Contain("25.00");
        taken.Body.Should().Contain("the payment was a duplicate");
        taken.Body.Should().Contain("the card or account you paid it in from");

        notices.Should().NotContain(notice => notice.Title == "Refund issued",
            "payer and payee are one member here, who gets one notice about one refund");
    }

    [Fact]
    public async Task The_requester_hears_a_refusal_with_the_note_and_an_appeal_reopens_it()
    {
        var (_, memberAuth) = await MemberAsync();
        var paymentId = await CapturedPurchaseAsync(15m);

        var requested = await HttpClient.PostAsJsonAsync(
            new Uri($"{Financial}/payment-intents/{paymentId}/refund-requests", UriKind.Relative),
            new { reason = "NotDelivered", note = "The boost never appeared on my listing." });
        requested.StatusCode.Should().Be(HttpStatusCode.OK, await requested.Content.ReadAsStringAsync());
        var requestId = (await ReadDataAsync<RefundRequestShape>(requested)).RefundRequestId;

        var (refuser, refuserAuth) = await SignInAsRoleAsync("Admin");

        var unexplained = await DecideRefundAsync(requestId, approve: false, note: null);
        unexplained.StatusCode.Should().Be(HttpStatusCode.BadRequest, await unexplained.Content.ReadAsStringAsync());
        (await CodeAsync(unexplained)).Should().Be(FinancialErrorCodes.RefundDecisionNoteRequired);

        (await DecideRefundAsync(requestId, approve: false, note: "The boost ran for its full seven days."))
            .EnsureSuccessStatusCode();

        await Factory.DrainOutboxAsync();

        ActAs(memberAuth);
        var refusal = (await BillingInboxAsync())
            .Should().ContainSingle(notice => notice.Title == "Your refund request was turned down").Subject;
        refusal.Body.Should().Contain("15.00");
        refusal.Body.Should().Contain("Diaspora's note: The boost ran for its full seven days.");
        refusal.Body.Should().Contain("Profile, Appeals");
        refusal.TargetRoute.Should().Be($"/wallet/payments/{paymentId}");

        var appealId = await FileMoneyAppealAsync(requestId, "The boost did not show for four of the seven days.");

        await SignInAsRoleAsync("Moderator");
        (await ReviewAsync(appealId, "Approve", "The placement log shows a four-day gap."))
            .EnsureSuccessStatusCode();

        await Factory.DrainOutboxAsync();
        await Factory.DrainOutboxAsync();

        using (var scope = Factory.Services.CreateScope())
        {
            var request = (await scope.ServiceProvider.GetRequiredService<IRefundRequestRepository>()
                .FindAsync(RefundRequestId.From(requestId)))!;

            request.Status.Should().Be(RefundRequestStatus.Requested, "the appeal reopened it for a new decision");
            request.ReopenedAt.Should().NotBeNull();
            request.PreviousDecidedByUserId.Should().Be(refuser);
        }

        // The member of staff who refused it does not get the second look.
        ActAs(refuserAuth);
        var same = await DecideRefundAsync(requestId, approve: false, note: "Still no, for the same reason.");
        same.StatusCode.Should().Be(HttpStatusCode.Forbidden, await same.Content.ReadAsStringAsync());
        (await CodeAsync(same)).Should().Be(FinancialErrorCodes.RefundRequestSameDecider);
    }

    // ── Promotional credit ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Nobody_grants_credit_to_themselves()
    {
        var (admin, _) = await SignInAsRoleAsync("Admin");

        var response = await GrantCreditAsync(admin, 10m);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
        (await CodeAsync(response)).Should().Be(FinancialErrorCodes.CreditGrantToSelf);
        (await PromotionalAsync(admin)).Should().Be(0);
    }

    [Fact]
    public async Task A_small_grant_lands_and_a_grant_above_the_cap_is_refused()
    {
        var (member, _) = await MemberAsync();
        await SignInAsRoleAsync("Admin");

        var small = await GrantCreditAsync(member, 20m);
        small.StatusCode.Should().Be(HttpStatusCode.OK, await small.Content.ReadAsStringAsync());
        (await PromotionalAsync(member)).Should().Be(2000);

        var huge = await GrantCreditAsync(member, 150m);
        huge.StatusCode.Should().Be(HttpStatusCode.Conflict, await huge.Content.ReadAsStringAsync());
        (await CodeAsync(huge)).Should().Be(FinancialErrorCodes.CreditGrantAboveMaximum);

        var unexplained = await GrantCreditAsync(member, 5m, reason: "bonus");
        unexplained.StatusCode.Should().Be(HttpStatusCode.BadRequest, await unexplained.Content.ReadAsStringAsync());
        (await CodeAsync(unexplained)).Should().Be(FinancialErrorCodes.CreditGrantReasonRequired);

        (await PromotionalAsync(member)).Should().Be(2000, "only the first grant landed");
    }

    [Fact]
    public async Task A_large_grant_waits_for_a_second_admin()
    {
        var (member, _) = await MemberAsync();
        var (asker, askerAuth) = await SignInAsRoleAsync("Admin");
        var (approver, approverAuth) = await SignInAsRoleAsync("Admin");

        ActAs(askerAuth);
        var direct = await GrantCreditAsync(member, 80m);
        direct.StatusCode.Should().Be(HttpStatusCode.Conflict, await direct.Content.ReadAsStringAsync());
        (await CodeAsync(direct)).Should().Be(FinancialErrorCodes.CreditGrantNeedsApproval);

        var requested = await HttpClient.PostAsJsonAsync(
            new Uri($"{Financial}/operations/credit/grant-requests", UriKind.Relative),
            CreditBody(member, 80m));
        requested.StatusCode.Should().Be(HttpStatusCode.OK, await requested.Content.ReadAsStringAsync());
        var request = await ReadDataAsync<CreditRequestShape>(requested);
        request.Status.Should().Be("Pending");
        request.RequestedByUserId.Should().Be(asker);
        (await PromotionalAsync(member)).Should().Be(0, "nothing lands on the asking");

        var queue = await ReadDataAsync<List<CreditRequestShape>>(await HttpClient.GetAsync(
            new Uri($"{Financial}/operations/credit/grant-requests", UriKind.Relative)));
        queue.Should().Contain(entry => entry.RequestId == request.RequestId);

        var selfApproved = await DecideCreditAsync(request.RequestId, approve: true, note: null);
        selfApproved.StatusCode.Should().Be(HttpStatusCode.Forbidden, await selfApproved.Content.ReadAsStringAsync());
        (await CodeAsync(selfApproved)).Should().Be(FinancialErrorCodes.CreditGrantSelfApproval);

        ActAs(approverAuth);
        var approved = await DecideCreditAsync(request.RequestId, approve: true, note: null);
        approved.StatusCode.Should().Be(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());

        var decided = await ReadDataAsync<CreditRequestShape>(approved);
        decided.Status.Should().Be("Approved");
        decided.DecidedByUserId.Should().Be(approver);
        decided.CreditGrantId.Should().NotBeNull();

        (await PromotionalAsync(member)).Should().Be(8000);

        var again = await DecideCreditAsync(request.RequestId, approve: true, note: null);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await PromotionalAsync(member)).Should().Be(8000, "a request lands once");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>A verified member with an active financial identity, signed in.</summary>
    private async Task<(Guid UserId, Diaspora.Contracts.Identity.V1.Responses.AuthenticationResponse Auth)> MemberAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        ActAs(auth);

        // The financial identity is opened by an integration event; see FinancialMovementTests.
        await Factory.DrainOutboxAsync();

        using var scope = Factory.Services.CreateScope();

        var accounts = scope.ServiceProvider.GetRequiredService<IFinancialAccountRepository>();
        var identities = scope.ServiceProvider.GetRequiredService<FinancialIdentityService>();

        var account = await accounts.FindByOwnerAsync(auth.User.Id, FinancialAccountType.Personal)
            ?? await identities.GetOrCreateAsync(auth.User.Id, FinancialAccountType.Personal, Currency);

        await identities.ActivateAsync(account);
        await identities.EnsureWalletsAsync(account, Currency);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        return (auth.User.Id, auth);
    }

    private void ActAs(Diaspora.Contracts.Identity.V1.Responses.AuthenticationResponse auth)
    {
        Api.Authenticate(auth.Tokens.AccessToken);
        HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Tokens.AccessToken);
    }

    private async Task<Guid> PrimaryWalletIdAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        var wallets = await scope.ServiceProvider.GetRequiredService<IWalletRepository>().ListByOwnerAsync(userId);

        return wallets.First(wallet =>
            wallet.WalletType == WalletType.Primary
            && string.Equals(wallet.Currency, Currency, StringComparison.Ordinal)).Id.Value;
    }

    private async Task<Wallet> WalletAsync(Guid walletId)
    {
        using var scope = Factory.Services.CreateScope();

        return (await scope.ServiceProvider.GetRequiredService<IWalletRepository>().FindAsync(WalletId.From(walletId)))!;
    }

    private async Task<long> AvailableAsync(Guid userId) =>
        (await WalletAsync(await PrimaryWalletIdAsync(userId))).AvailableMinorUnits;

    private async Task<long> PromotionalAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        var wallets = await scope.ServiceProvider.GetRequiredService<IWalletRepository>().ListByOwnerAsync(userId);

        return wallets.FirstOrDefault(wallet =>
            wallet.WalletType == WalletType.Promotional
            && string.Equals(wallet.Currency, Currency, StringComparison.Ordinal))?.AvailableMinorUnits ?? 0;
    }

    private Task<HttpResponseMessage> SetWalletStatusAsync(Guid walletId, string status, string? reason) =>
        HttpClient.PutAsJsonAsync(
            new Uri($"{Financial}/wallets/{walletId}/status", UriKind.Relative),
            new { status, reason });

    private Task<HttpResponseMessage> DecideRefundAsync(Guid requestId, bool approve, string? note) =>
        HttpClient.PostAsJsonAsync(
            new Uri($"{Financial}/operations/refund-requests/{requestId}/decide", UriKind.Relative),
            new { approve, decisionNote = note });

    private static object CreditBody(Guid userId, decimal amount, string reason = "Compensation for the outage on 3 November.") =>
        new { userId, amount, currency = Currency, kind = "Compensation", reason };

    private Task<HttpResponseMessage> GrantCreditAsync(
        Guid userId, decimal amount, string reason = "Compensation for the outage on 3 November.") =>
        HttpClient.PostAsJsonAsync(
            new Uri($"{Financial}/operations/credit", UriKind.Relative), CreditBody(userId, amount, reason));

    private Task<HttpResponseMessage> DecideCreditAsync(Guid requestId, bool approve, string? note) =>
        HttpClient.PostAsJsonAsync(
            new Uri($"{Financial}/operations/credit/grant-requests/{requestId}/decide", UriKind.Relative),
            new { approve, note });

    private async Task<Guid> FileMoneyAppealAsync(Guid targetId, string reason)
    {
        var filed = await HttpClient.PostAsJsonAsync(
            "/api/v1/trust/appeals",
            new { type = "Money", targetId = targetId.ToString(), reason },
            JsonOptions);

        filed.StatusCode.Should().Be(HttpStatusCode.Created, await filed.Content.ReadAsStringAsync());

        return JsonDocument.Parse(await filed.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("appealId").GetGuid();
    }

    private Task<HttpResponseMessage> ReviewAsync(Guid appealId, string decision, string explanation) =>
        HttpClient.PostAsJsonAsync(
            $"/api/v1/trust/appeals/{appealId}/review",
            new { decision, explanation },
            JsonOptions);

    private async Task<Guid> CreateDepositAsync(decimal amount)
    {
        var response = await HttpClient.PostAsJsonAsync(
            new Uri($"{Financial}/wallets/me/deposits", UriKind.Relative),
            new { amount, currency = Currency, methodKind = "Card", idempotencyKey = Guid.NewGuid().ToString("N") });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return (await ReadDataAsync<PaymentShape>(response)).PaymentIntentId;
    }

    /// <summary>An ordinary purchase, paid by card and captured, as the caller.</summary>
    private async Task<Guid> CapturedPurchaseAsync(decimal amount)
    {
        var response = await HttpClient.PostAsJsonAsync(
            new Uri($"{Financial}/payment-intents", UriKind.Relative),
            new { amount, currency = Currency, methodKind = "Card", description = "A boost for a listing" });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var paymentId = (await ReadDataAsync<PaymentShape>(response)).PaymentIntentId;

        await ConfirmByWebhookAsync(paymentId, (long)(amount * 100));

        return paymentId;
    }

    /// <summary>Confirms a payment the way a provider does: authorised, then captured, both signed.</summary>
    private async Task ConfirmByWebhookAsync(Guid paymentIntentId, long minorUnits)
    {
        await PostWebhookAsync(WebhookBody("authorized", paymentIntentId, minorUnits));
        await PostWebhookAsync(WebhookBody("captured", paymentIntentId, minorUnits));
    }

    private static string WebhookBody(string kind, Guid reference, long minorUnits) =>
        "{"
        + $"\"eventKind\":\"{kind}\","
        + $"\"externalReference\":\"{reference}\","
        + $"\"eventId\":\"{Guid.NewGuid():N}\","
        + $"\"amountMinorUnits\":{minorUnits},"
        + $"\"currency\":\"{Currency}\"}}";

    private async Task PostWebhookAsync(string body)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"{Financial}/webhooks/{SimulatedPaymentGateway.ProviderName}", UriKind.Relative))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        request.Headers.Add(
            SimulatedPaymentWebhookProcessor.SignatureHeader,
            SimulatedPaymentWebhookProcessor.Sign(body));

        var response = await HttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    /// <summary>The caller's billing notices, as rendered in the inbox.</summary>
    private async Task<List<NoticeShape>> BillingInboxAsync()
    {
        var response = await HttpClient.GetAsync("/api/v1/notifications?category=billing&limit=50");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var notices = new List<NoticeShape>();
        Collect(document.RootElement, notices);
        return notices;
    }

    /// <summary>The push lines stored for a member under one template. The inbox does not serve them.</summary>
    private async Task<List<string>> ShortBodiesAsync(Guid userId, string templateKey)
    {
        using var scope = Factory.Services.CreateScope();

        var notifications = await scope.ServiceProvider.GetRequiredService<DiasporaDbContext>()
            .Set<Notification>()
            .AsNoTracking()
            .Where(notification => notification.TemplateKey == templateKey
                && notification.RecipientUserId == UserId.From(userId))
            .ToListAsync();

        return [.. notifications.Select(notification => notification.Content.ShortBody ?? string.Empty)];
    }

    private static void Collect(JsonElement element, List<NoticeShape> into)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("title", out var title) && element.TryGetProperty("body", out var body))
                {
                    into.Add(new NoticeShape(
                        title.GetString() ?? string.Empty,
                        body.GetString() ?? string.Empty,
                        element.TryGetProperty("targetRoute", out var route) ? route.GetString() : null));
                    return;
                }

                foreach (var property in element.EnumerateObject())
                {
                    Collect(property.Value, into);
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Collect(item, into);
                }

                break;
        }
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

    private sealed record PaymentShape(Guid PaymentIntentId, string Status);

    private sealed record RefundRequestShape(Guid RefundRequestId, string Status);

    private sealed record CreditRequestShape(
        Guid RequestId, string Status, Guid RequestedByUserId, Guid? DecidedByUserId, Guid? CreditGrantId);

    private sealed record NoticeShape(string Title, string Body, string? TargetRoute);
}
