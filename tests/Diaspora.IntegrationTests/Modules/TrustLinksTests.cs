using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Three defects between an email and the trust centre, found together on 20 Oct 2026.
/// <list type="number">
/// <item>An email's "Open in Diaspora" button pointed at <c>{api}/profile/trust-center</c>, a path
/// the API serves nothing at: a 404 in the phone's browser. It now points at
/// <c>{api}/open/profile/trust-center</c>, a page that hands off to <c>diaspora://</c>.</item>
/// <item>The trust profile reported every type a member had never attempted as <c>Pending</c>,
/// the same word as a case awaiting review. Never-attempted types now read <c>NotStarted</c>.</item>
/// <item>Verifying an email told Trust nothing, so the Email row was never done. Identity now
/// publishes the fact and Trust mirrors it as an approved Email verification.</item>
/// </list>
/// </summary>
[Collection(IdentityCollection.Name)]
public sealed class TrustLinksTests : PooledIdentityTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public TrustLinksTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── The landing page ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_open_page_hands_a_known_route_to_the_app()
    {
        var response = await HttpClient.GetAsync("/open/profile/trust-center");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");

        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("diaspora://profile/trust-center");
        html.Should().Contain("location.replace(");
        html.Should().Contain("Open Diaspora");
    }

    [Fact]
    public async Task The_open_page_needs_no_session()
    {
        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync("/open/housing/123");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("diaspora://housing/123");
    }

    [Theory]
    [InlineData("/open/evil.com")]
    [InlineData("/open/evil.com/profile")]
    [InlineData("/open//evil.com")]
    [InlineData("/open/https:/evil.com")]
    [InlineData("/open/profile/../../evil")]
    [InlineData("/open/profiles")]
    [InlineData("/open")]
    [InlineData("/open/")]
    public async Task The_open_page_refuses_anything_that_is_not_an_app_route(string path)
    {
        var response = await HttpClient.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var html = await response.Content.ReadAsStringAsync();
        html.Should().NotContain("diaspora://");
        html.Should().NotContain("location.replace(");
    }

    [Fact]
    public async Task The_open_page_escapes_what_it_echoes()
    {
        var response = await HttpClient.GetAsync("/open/job/%22%3E%3Cscript%3Ealert(1)%3C%2Fscript%3E");

        var html = await response.Content.ReadAsStringAsync();
        html.Should().NotContain("<script>alert(1)</script>");
        html.Should().NotContain("\"><script>");
    }

    [Fact]
    public async Task A_query_string_is_carried_to_the_app_and_stays_encoded()
    {
        var kept = await (await HttpClient.GetAsync("/open/search?q=lagos&tab=jobs")).Content.ReadAsStringAsync();
        kept.Should().Contain("diaspora://search?q=lagos&amp;tab=jobs");

        var encoded = await HttpClient.GetAsync("/open/search?q=%22%3E%3Cscript%3Ealert(1)%3C%2Fscript%3E");
        encoded.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await encoded.Content.ReadAsStringAsync();
        html.Should().Contain("diaspora://search?q=%22%3E%3Cscript%3E");
        html.Should().NotContain("<script>alert");
    }

    // ── NotStarted versus Pending ────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_type_never_attempted_reads_NotStarted_and_a_submitted_one_reads_Pending()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var before = await VerificationsAsync();
        before.GetProperty("Diaspora").GetString().Should().Be("NotStarted");
        before.GetProperty("Identity").GetString().Should().Be("NotStarted");
        before.EnumerateObject().Select(entry => entry.Value.GetString())
            .Should().NotContain("Pending", "nothing has been submitted");

        var submitted = await HttpClient.PostAsJsonAsync(
            "/api/v1/trust/verifications/diaspora",
            new
            {
                nationality = "NG",
                residenceCountry = "GB",
                documentType = "Passport",
                issueCountry = "NG",
                issueDate = "2020-01-01",
                expiryDate = "2030-01-01",
                documentNumber = "AA0012345",
                documentReference = "storage/doc-1",
                selfieReference = "storage/selfie-1"
            },
            JsonOptions);
        submitted.StatusCode.Should().Be(HttpStatusCode.Created, await submitted.Content.ReadAsStringAsync());

        var after = await VerificationsAsync();
        after.GetProperty("Diaspora").GetString().Should().Be("Pending");
        after.GetProperty("Identity").GetString().Should().Be("NotStarted");
    }

    // ── Email verified → Trust ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Verifying_an_email_approves_the_Email_verification_in_trust()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        await Factory.DrainOutboxAsync();

        (await VerificationsAsync()).GetProperty("Email").GetString().Should().Be("NotStarted");

        await ConfirmEmailAsync(auth.User.Id, auth.User.Email!);
        await Factory.DrainOutboxAsync();

        (await VerificationsAsync()).GetProperty("Email").GetString().Should().Be("Approved");

        var profile = await TrustProfileAsync(auth.User.Id);
        profile!.Grants.Should().Contain(VerificationType.Email);
    }

    [Fact]
    public async Task A_redelivered_email_verification_pays_once()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        await ConfirmEmailAsync(auth.User.Id, auth.User.Email!);
        await Factory.DrainOutboxAsync();
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Admin");
        var repaired = await HttpClient.PostAsync(
            $"/api/v1/admin/accounts/{auth.User.Id}/republish-email-verification", content: null);
        repaired.IsSuccessStatusCode.Should().BeTrue(await repaired.Content.ReadAsStringAsync());
        await Factory.DrainOutboxAsync();

        (await EventsAsync(auth.User.Id))
            .Count(entry => entry.ActionType == ReputationActions.EmailVerified)
            .Should().Be(1);

        (await TrustProfileAsync(auth.User.Id))!.Badges
            .Count(badge => badge.Badge == BadgeType.EmailVerified && badge.IsActive)
            .Should().Be(1);
    }

    /// <summary>
    /// The backfill for members verified between 5 and 20 Oct 2026, whose verification published
    /// nothing. Simulated by verifying and then discarding the event before Trust hears it.
    /// </summary>
    [Fact]
    public async Task Re_announcing_mirrors_an_email_that_was_verified_into_silence()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        await Factory.DrainOutboxAsync();

        await ConfirmEmailAsync(auth.User.Id, auth.User.Email!);
        await DiscardPendingEmailEventsAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
        (await VerificationsAsync()).GetProperty("Email").GetString().Should().Be("NotStarted");

        await SignInAsRoleAsync("Admin");
        var before = await PendingEmailEventsAsync();
        var repaired = await HttpClient.PostAsync(
            $"/api/v1/admin/accounts/{auth.User.Id}/republish-email-verification", content: null);
        repaired.IsSuccessStatusCode.Should().BeTrue(await repaired.Content.ReadAsStringAsync());
        (await PendingEmailEventsAsync()).Should().Be(before + 1, "a 200 must mean a message was enqueued");

        await Factory.DrainOutboxAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
        (await VerificationsAsync()).GetProperty("Email").GetString().Should().Be("Approved");
    }

    [Fact]
    public async Task Re_announcing_refuses_a_member_whose_email_is_not_verified()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        await SignInAsRoleAsync("Admin");

        var response = await HttpClient.PostAsync(
            $"/api/v1/admin/accounts/{auth.User.Id}/republish-email-verification", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private async Task<JsonElement> VerificationsAsync()
    {
        var response = await HttpClient.GetAsync("/api/v1/trust/profile");
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(body);

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("data").GetProperty("verifications").Clone();
    }

    /// <summary>Verifies the address through the real path: a signed link, then the confirm route.</summary>
    private async Task ConfirmEmailAsync(Guid userId, string address)
    {
        string token;
        using (var scope = Factory.Services.CreateScope())
        {
            token = scope.ServiceProvider.GetRequiredService<IEmailVerificationLinks>()
                .Create(UserId.From(userId), address).Token;
        }

        var response = await HttpClient.PostAsJsonAsync("/api/v1/auth/email/confirm", new { token });
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(body);
        body.Should().Contain("Verified");
    }

    private async Task<int> PendingEmailEventsAsync()
    {
        using var scope = Factory.Services.CreateScope();

        var pending = await scope.ServiceProvider
            .GetRequiredService<IOutboxQueries>()
            .ListPendingAsync(new OutboxQuery(
                EventType: typeof(UserEmailVerifiedIntegrationEvent).FullName, Limit: 200));

        return pending.Count;
    }

    private async Task DiscardPendingEmailEventsAsync()
    {
        using var scope = Factory.Services.CreateScope();

        var queries = scope.ServiceProvider.GetRequiredService<IOutboxQueries>();
        var pending = await queries.ListPendingAsync(new OutboxQuery(
            EventType: typeof(UserEmailVerifiedIntegrationEvent).FullName, Limit: 200));

        pending.Should().NotBeEmpty("confirming the address publishes the fact");

        // Marked delivered without being delivered: the state of a verification made before
        // anything published it, which no consumer ever saw.
        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
        foreach (var message in pending)
        {
            await store.MarkDeliveredAsync(message.Sequence, DateTimeOffset.UtcNow);
        }
    }

    private async Task<TrustProfile?> TrustProfileAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<ITrustProfileRepository>()
            .GetByUserAsync(UserId.From(userId));
    }

    private async Task<IReadOnlyList<ReputationEvent>> EventsAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        var page = await scope.ServiceProvider
            .GetRequiredService<IReputationProfileRepository>()
            .ListEventsAsync(UserId.From(userId), new PaginationRequest { PageSize = 50 });

        return page.Items;
    }
}
