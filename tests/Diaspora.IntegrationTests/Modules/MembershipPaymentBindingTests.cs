using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Financial.Application.Memberships;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Accounts;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Infrastructure.Hosting;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Everything a membership-payment test needs, over a host the derived class chooses.
/// <para>
/// Generic for one reason only: the concurrency test cannot run against the single shared SQLite
/// connection every other suite uses, and duplicating four hundred lines of setup to change one line
/// of database arrangement is how two copies of a helper start to drift.
/// </para>
/// </summary>
public abstract class MembershipPaymentBindingTestBase<TFactory> : IdentityTestBase<TFactory>
    where TFactory : CustomWebApplicationFactory, new()
{
    protected const string Communities = "/api/v1/communities";
    protected const string CommunityLife = "/api/v1/community-life";
    protected const string Financial = "/api/v1/financial";

    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected MembershipPaymentBindingTestBase(TFactory factory) : base(factory)
    {
    }

    protected void Authorize(string accessToken) =>
        HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

    protected async Task<TestMember> MemberAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
        Authorize(auth.Tokens.AccessToken);

        await Factory.DrainOutboxAsync();
        await ActivateFinancialIdentityAsync(auth.User.Id);

        return new TestMember(auth.User.Id, auth.Tokens.AccessToken);
    }

    /// <summary>
    /// Puts the caller's financial identity into the state Trust puts it in.
    /// <para>
    /// Arrangement rather than a bypass, and the same call the Trust consumer makes. A financial
    /// account opens <c>Pending</c> at registration and is activated by an event only the real Trust
    /// Engine publishes; without this, every payment below would be asserting a rule this file is not
    /// about.
    /// </para>
    /// </summary>
    protected async Task ActivateFinancialIdentityAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        var accounts = scope.ServiceProvider.GetRequiredService<IFinancialAccountRepository>();
        var identities = scope.ServiceProvider.GetRequiredService<FinancialIdentityService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var account = await accounts.FindByOwnerAsync(userId, FinancialAccountType.Personal)
            ?? await identities.GetOrCreateAsync(userId, FinancialAccountType.Personal, "GBP");

        await identities.ActivateAsync(account);
        await unitOfWork.SaveChangesAsync();
    }

    /// <summary>An organization, a paid tier and an approved application. All through the real routes.</summary>
    protected async Task<MembershipShape> ApprovedMembershipAsync()
    {
        var organizationId = await CreateOrganizationAsync();
        var programme = await SaveProgrammeAsync(organizationId);
        var tierId = programme.Tiers.Single(tier => tier.Price is not null).TierId;

        var membership = await ReadDataAsync<MembershipShape>(
            await HttpClient.PostAsJsonAsync(
                new Uri($"{Communities}/{organizationId}/memberships/applications", UriKind.Relative),
                new { tierId }));

        // The organization approves. The caller is its owner, which is what creating it made them.
        var approved = await ReadDataAsync<MembershipShape>(
            await HttpClient.PostAsJsonAsync(
                new Uri(
                    $"{Communities}/memberships/{membership.MembershipId}/decision",
                    UriKind.Relative),
                new { decision = "Approve" }));

        approved.Status.Should().Be("Approved", "a priced tier stops here until the money arrives");

        return approved;
    }

    protected async Task<Guid> CreateOrganizationAsync() =>
        (await ReadDataAsync<OrganizationShape>(
            await HttpClient.PostAsJsonAsync(
                new Uri($"{CommunityLife}/organizations", UriKind.Relative),
                new
                {
                    name = $"Association {Guid.NewGuid():N}",
                    summary = "A body created by the integration suite.",
                    organizationTypeKey = "cultural-association",
                    countryCode = "GB",
                    cityName = "London"
                }))).OrganizationId;

    protected async Task<ProgrammeShape> SaveProgrammeAsync(Guid organizationId) =>
        await ReadDataAsync<ProgrammeShape>(
            await HttpClient.PutAsJsonAsync(
                new Uri($"{Communities}/{organizationId}/membership", UriKind.Relative),
                new
                {
                    summary = "Join us.",
                    visibility = "Public",
                    isOpen = true,
                    requiresApproval = true,
                    tiers = new object[]
                    {
                        new
                        {
                            name = "Patron",
                            description = "The paid one.",
                            priceAmount = 25m,
                            currency = "GBP",
                            billingPeriod = "Yearly",
                            sortOrder = 0
                        }
                    }
                }));

    protected async Task<Guid> CreateIntentAsync(
        Guid subjectId, decimal amount, string subjectType = "CommunityMembership") =>
        (await ReadDataAsync<PaymentShape>(
            await HttpClient.PostAsJsonAsync(
                new Uri($"{Financial}/payment-intents", UriKind.Relative),
                new
                {
                    amount,
                    currency = "GBP",
                    methodKind = "Card",
                    description = "Membership",
                    subjectType,
                    subjectId,
                    idempotencyKey = Guid.NewGuid().ToString("N")
                }))).PaymentIntentId;

    protected Task<HttpResponseMessage> AuthorizePaymentAsync(Guid intentId) =>
        HttpClient.PostAsJsonAsync(
            new Uri($"{Financial}/payment-intents/{intentId}/authorize", UriKind.Relative),
            new { });

    protected Task<HttpResponseMessage> CapturePaymentAsync(Guid intentId) =>
        HttpClient.PostAsJsonAsync(
            new Uri($"{Financial}/payment-intents/{intentId}/capture", UriKind.Relative),
            new { });

    protected async Task<Guid> SettledPaymentForAsync(Guid membershipId, decimal amount)
    {
        var intent = await CreateIntentAsync(membershipId, amount);

        (await AuthorizePaymentAsync(intent)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await CapturePaymentAsync(intent)).StatusCode.Should().Be(HttpStatusCode.OK);

        return intent;
    }

    protected Task<HttpResponseMessage> BindAsync(Guid membershipId, Guid paymentIntentId) =>
        HttpClient.PostAsJsonAsync(
            new Uri($"{Communities}/memberships/{membershipId}/payments", UriKind.Relative),
            new { paymentIntentId });

    protected async Task<MembershipShape> MembershipAsync(Guid membershipId) =>
        await ReadDataAsync<MembershipShape>(
            await HttpClient.GetAsync(
                new Uri($"{Communities}/memberships/{membershipId}", UriKind.Relative)));

    protected async Task<List<BindingShape>> ListBindingsAsync(Guid membershipId) =>
        await ReadDataAsync<List<BindingShape>>(
            await HttpClient.GetAsync(
                new Uri($"{Financial}/memberships/{membershipId}/payments", UriKind.Relative)));

    protected static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        return payload.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }

    protected sealed record TestMember(Guid UserId, string AccessToken);

    protected sealed record OrganizationShape(Guid OrganizationId);

    protected sealed record ProgrammeShape(List<TierShape> Tiers);

    protected sealed record TierShape(Guid TierId, MoneyShape? Price);

    protected sealed record MoneyShape(decimal Amount, string Currency, long MinorUnits);

    protected sealed record MembershipShape(Guid MembershipId, string Status, Guid? PaymentIntentId);

    protected sealed record PaymentShape(Guid PaymentIntentId, string Status);

    protected sealed record BindingShape(
        Guid BindingId,
        Guid MembershipId,
        Guid PaymentIntentId,
        string PaymentStatus,
        MoneyShape Amount,
        bool IsNewBinding);
}

/// <summary>
/// COMMUNITY-LIFE-GAP-003, over real HTTP: a paid membership activates on settlement and on nothing
/// else.
/// <para>
/// <b>Every payment here is a real B15 payment intent</b> — created, authorised and captured through
/// the certified routes, with the simulated gateway's own outcomes. Nothing stages a captured payment
/// directly, because a test that did would prove the binding works against a state no gateway
/// produces.
/// </para>
/// <para>
/// <b>The refusals are the point of this file.</b> A route that activated a membership on request
/// alone would let any member grant themselves a paid one, and the register says so in as many words.
/// Each of the seven below is a different way of asking it to.
/// </para>
/// </summary>
public sealed class MembershipPaymentBindingTests
    : MembershipPaymentBindingTestBase<CustomWebApplicationFactory>
{
    public MembershipPaymentBindingTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    // ── The happy path ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The gap, closed.</b> A settled payment binds, the membership activates, and the binding is
    /// a record B15 owns rather than a claim the client made.
    /// </summary>
    [Fact]
    public async Task A_settled_payment_binds_and_activates_the_membership()
    {
        await MemberAsync();

        var membership = await ApprovedMembershipAsync();
        var intent = await SettledPaymentForAsync(membership.MembershipId, 25m);

        var response = await BindAsync(membership.MembershipId, intent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var bound = await ReadDataAsync<MembershipShape>(response);

        bound.Status.Should().Be("Active");
        bound.PaymentIntentId.Should().Be(intent);
    }

    /// <summary>
    /// The financial record is B15's and is readable from B15's own surface.
    /// <para>
    /// The amount is the intent's captured amount, read back — never a number the client sent, because
    /// the binding request carries no number at all.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_binding_is_recorded_on_the_financial_side()
    {
        await MemberAsync();

        var membership = await ApprovedMembershipAsync();
        var intent = await SettledPaymentForAsync(membership.MembershipId, 25m);

        await BindAsync(membership.MembershipId, intent);

        var bindings = await ListBindingsAsync(membership.MembershipId);

        bindings.Should().HaveCount(1);
        bindings[0].PaymentIntentId.Should().Be(intent);
        bindings[0].Amount.MinorUnits.Should().Be(2_500);
        bindings[0].PaymentStatus.Should().Be("Captured");
    }

    // ── The refusals ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Authorised is not settled.</b> Money reserved is not money taken and the capture can still
    /// fail — which is exactly what §8 forbids the client concluding from.
    /// </summary>
    [Fact]
    public async Task An_authorized_but_uncaptured_payment_is_refused()
    {
        await MemberAsync();

        var membership = await ApprovedMembershipAsync();
        var intent = await CreateIntentAsync(membership.MembershipId, 25m);

        await AuthorizePaymentAsync(intent);

        var response = await BindAsync(membership.MembershipId, intent);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MembershipAsync(membership.MembershipId)).Status.Should().Be("Approved");
    }

    /// <summary>A payment the gateway declined leaves the membership exactly where it was.</summary>
    [Fact]
    public async Task A_failed_payment_is_refused()
    {
        await MemberAsync();

        var membership = await ApprovedMembershipAsync();

        // .01 is the simulated gateway's decline. A real decline, through the real authorize route.
        var intent = await CreateIntentAsync(membership.MembershipId, 25.01m);

        await AuthorizePaymentAsync(intent);

        var response = await BindAsync(membership.MembershipId, intent);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MembershipAsync(membership.MembershipId)).Status.Should().Be("Approved");
    }

    /// <summary>A cancelled payment is terminal and buys nothing.</summary>
    [Fact]
    public async Task A_cancelled_payment_is_refused()
    {
        await MemberAsync();

        var membership = await ApprovedMembershipAsync();
        var intent = await CreateIntentAsync(membership.MembershipId, 25m);

        (await HttpClient.PostAsJsonAsync(
                new Uri($"{Financial}/payment-intents/{intent}/cancel", UriKind.Relative), new { }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await BindAsync(membership.MembershipId, intent);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MembershipAsync(membership.MembershipId)).Status.Should().Be("Approved");
    }

    /// <summary>
    /// A payment made for something else cannot be redirected at a membership.
    /// <para>
    /// The subject on the intent is what makes this refusable at all: without it, any captured
    /// payment of the caller's would activate any membership they held.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_payment_made_for_something_else_is_refused()
    {
        await MemberAsync();

        var membership = await ApprovedMembershipAsync();

        // A perfectly good captured payment — for a marketplace listing.
        var intent = await CreateIntentAsync(Guid.NewGuid(), 25m, subjectType: "MarketplaceListing");

        await AuthorizePaymentAsync(intent);
        await CapturePaymentAsync(intent);

        var response = await BindAsync(membership.MembershipId, intent);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MembershipAsync(membership.MembershipId)).Status.Should().Be("Approved");
    }

    /// <summary>A settled payment naming a different membership does not activate this one.</summary>
    [Fact]
    public async Task A_payment_for_another_membership_is_refused()
    {
        await MemberAsync();

        var membership = await ApprovedMembershipAsync();
        var intent = await SettledPaymentForAsync(Guid.NewGuid(), 25m);

        var response = await BindAsync(membership.MembershipId, intent);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MembershipAsync(membership.MembershipId)).Status.Should().Be("Approved");
    }

    /// <summary>
    /// Somebody else's payment cannot buy your membership.
    /// <para>
    /// Answered 404 rather than 403, so the route cannot be used to discover which payment
    /// identifiers exist.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_payment_belonging_to_another_member_is_refused()
    {
        await MemberAsync();

        var strangersMembership = await ApprovedMembershipAsync();
        var strangersIntent = await SettledPaymentForAsync(strangersMembership.MembershipId, 25m);

        await MemberAsync();

        var membership = await ApprovedMembershipAsync();

        var response = await BindAsync(membership.MembershipId, strangersIntent);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await MembershipAsync(membership.MembershipId)).Status.Should().Be("Approved");
    }

    /// <summary>Your own payment cannot buy somebody else's membership either.</summary>
    [Fact]
    public async Task A_membership_belonging_to_another_member_is_refused()
    {
        var stranger = await MemberAsync();
        var strangersMembership = await ApprovedMembershipAsync();

        await MemberAsync();

        // A settled payment of the caller's own, naming somebody else's membership.
        var intent = await SettledPaymentForAsync(strangersMembership.MembershipId, 25m);

        var response = await BindAsync(strangersMembership.MembershipId, intent);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        Api.Authenticate(stranger.AccessToken);
        Authorize(stranger.AccessToken);

        (await MembershipAsync(strangersMembership.MembershipId)).Status.Should().Be("Approved");
    }

    /// <summary>An anonymous caller cannot bind anything.</summary>
    [Fact]
    public async Task Binding_requires_authentication()
    {
        await MemberAsync();

        var membership = await ApprovedMembershipAsync();

        HttpClient.DefaultRequestHeaders.Authorization = null;

        var response = await BindAsync(membership.MembershipId, Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Idempotency ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A retried binding is answered with the binding that exists, not with a conflict.
    /// <para>
    /// <b>200 rather than 409, deliberately.</b> A mobile retry is the same request twice; answering
    /// the second with a conflict would show the member a failed payment they had in fact made.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Binding_the_same_payment_twice_is_safe()
    {
        await MemberAsync();

        var membership = await ApprovedMembershipAsync();
        var intent = await SettledPaymentForAsync(membership.MembershipId, 25m);

        (await BindAsync(membership.MembershipId, intent)).StatusCode
            .Should().Be(HttpStatusCode.OK);

        var second = await BindAsync(membership.MembershipId, intent);

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDataAsync<MembershipShape>(second)).Status.Should().Be("Active");

        (await ListBindingsAsync(membership.MembershipId))
            .Should().HaveCount(1, "a retry records nothing new");
    }

    // ── Reconciliation ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Money captured, membership not activated — found and repaired.</b>
    /// <para>
    /// The binding is recorded through B15's own service, which is what the HTTP route does first;
    /// the activation that ordinarily follows is skipped, which is exactly what a crash, a deploy or
    /// a database failure between the two transactions produces. The sweep finds the discrepancy, the
    /// replayed settlement activates the membership, and nothing is charged twice.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_capture_whose_activation_never_landed_is_reconciled()
    {
        var member = await MemberAsync();
        var membership = await ApprovedMembershipAsync();
        var intent = await SettledPaymentForAsync(membership.MembershipId, 25m);

        await BindWithoutActivatingAsync(member.AccessToken, membership.MembershipId, intent);

        (await MembershipAsync(membership.MembershipId)).Status
            .Should().Be("Approved", "the activation half never ran");

        (await RunReconciliationAsync()).Should().Be(1);

        await Factory.DrainOutboxAsync();

        (await MembershipAsync(membership.MembershipId)).Status.Should().Be("Active");
    }

    /// <summary>
    /// A membership that is already active is not re-announced.
    /// <para>
    /// The sweep asks the owning module before it shouts. Without that it would replay every binding
    /// on the platform every hour, and the log an operator watches for real repairs would be noise.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Reconciliation_leaves_an_already_activated_membership_alone()
    {
        await MemberAsync();

        var membership = await ApprovedMembershipAsync();
        var intent = await SettledPaymentForAsync(membership.MembershipId, 25m);

        await BindAsync(membership.MembershipId, intent);

        (await RunReconciliationAsync()).Should().Be(0);
    }

    /// <summary>
    /// Records the binding without the activation that ordinarily follows it.
    /// <para>
    /// This is what the platform looks like after a crash between B15's transaction and B18's: the
    /// money is captured, the binding exists, and the member is not a member.
    /// </para>
    /// </summary>
    private async Task BindWithoutActivatingAsync(
        string accessToken, Guid membershipId, Guid paymentIntentId)
    {
        using var scope = Factory.Services.CreateScope();

        ScopedCurrentUser.Apply(scope.ServiceProvider, accessToken);

        var binder = scope.ServiceProvider.GetRequiredService<MembershipPaymentBinder>();
        var bound = await binder.BindAsync(membershipId, paymentIntentId);

        bound.IsSuccess.Should().BeTrue(bound.IsFailure ? bound.Error.Description : null);
    }

    private Task<int> RunReconciliationAsync() =>
        Factory.Services
            .GetServices<IHostedService>()
            .OfType<MembershipBindingReconciliationSweep>()
            .Single()
            .RunOnceAsync();
}

/// <summary>
/// Two callers binding one payment at the same instant.
/// <para>
/// <b>Its own host, because the shared single SQLite connection cannot serve two requests at
/// once</b> — and the failure it produces is a 500 raised in the authentication middleware, which
/// looks exactly like a product defect and is not one. See
/// <see cref="ConcurrentWebApplicationFactory"/>.
/// </para>
/// </summary>
public sealed class MembershipPaymentBindingConcurrencyTests
    : MembershipPaymentBindingTestBase<ConcurrentWebApplicationFactory>
{
    public MembershipPaymentBindingConcurrencyTests(ConcurrentWebApplicationFactory factory)
        : base(factory)
    {
    }

    /// <summary>
    /// One payment, one binding, however many callers ask at once.
    /// <para>
    /// The unique index on the payment intent decides this, not a check in a handler — the window
    /// between a read and an insert is exactly where a double-tapped pay button lands. The membership
    /// ends active once, and the two responses agree.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Two_simultaneous_bindings_produce_one_binding()
    {
        await MemberAsync();

        var membership = await ApprovedMembershipAsync();
        var intent = await SettledPaymentForAsync(membership.MembershipId, 25m);

        var attempts = await Task.WhenAll(
            BindAsync(membership.MembershipId, intent),
            BindAsync(membership.MembershipId, intent));

        // Both, not just one. The loser of the race asked a question that has a true answer, and a
        // 500 for it would tell a member their payment had failed when it had not — which is the
        // failure the detach in MembershipPaymentBinder exists to prevent.
        attempts.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.OK);

        (await ListBindingsAsync(membership.MembershipId))
            .Should().HaveCount(1, "the unique index refuses the second insert");

        (await MembershipAsync(membership.MembershipId)).Status.Should().Be("Active");
    }
}
