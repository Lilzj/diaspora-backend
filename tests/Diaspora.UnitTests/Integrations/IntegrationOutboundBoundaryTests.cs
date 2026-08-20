using Diaspora.Modules.Integrations;
using Diaspora.Modules.Integrations.Domain.Enums;
using Diaspora.Modules.Integrations.Domain.Errors;
using Diaspora.Modules.Integrations.Extensibility.Http;
using Diaspora.Modules.Integrations.Extensibility.Providers;
using Diaspora.Modules.Integrations.Infrastructure.Http;
using Diaspora.Modules.Integrations.Infrastructure.Providers;
using Diaspora.Modules.Integrations.Options;
using Diaspora.Platform.Operations.Resilience;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Integrations;

/// <summary>
/// The outbound boundary: what may be called, and what an unconfigured deployment answers.
/// <para>
/// <b>Both halves are the same rule seen from two sides.</b> The allow-list decides what this
/// platform will reach; the not-configured defaults decide what it says when it can reach nothing.
/// A defect in either is silent: a permissive allow-list is an SSRF nobody notices until it is used,
/// and a default that fabricated a success would tell a member their event is in a calendar it is
/// not.
/// </para>
/// </summary>
public sealed class IntegrationOutboundBoundaryTests
{
    // ── SSRF ────────────────────────────────────────────────────────────────────────────────

    /// <summary>An allow-listed host over HTTPS is the only shape that passes.</summary>
    [Fact]
    public void An_allow_listed_https_host_is_permitted()
    {
        Gateway("oauth.provider.test")
            .IsAllowed(new Uri("https://oauth.provider.test/token"))
            .Should().BeTrue();
    }

    /// <summary>
    /// Everything a metadata-service SSRF looks like is refused.
    /// <para>
    /// A literal address cannot be allow-listed meaningfully and is the shape every one of these
    /// takes. Providers are named, so refusing literals costs nothing.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("https://169.254.169.254/latest/meta-data/")]
    [InlineData("https://127.0.0.1/token")]
    [InlineData("https://[::1]/token")]
    [InlineData("https://10.0.0.5/internal")]
    public void A_literal_address_is_always_refused(string url)
    {
        // Even with the host itself allow-listed, which is the configuration mistake this guards.
        Gateway("169.254.169.254", "127.0.0.1", "[::1]", "10.0.0.5")
            .IsAllowed(new Uri(url))
            .Should().BeFalse();
    }

    /// <summary>
    /// Plaintext is refused, because a provider token on an unencrypted connection is a token on the
    /// wire.
    /// </summary>
    [Fact]
    public void A_plaintext_scheme_is_refused()
    {
        Gateway("oauth.provider.test")
            .IsAllowed(new Uri("http://oauth.provider.test/token"))
            .Should().BeFalse();
    }

    /// <summary>
    /// Credentials in the authority are refused.
    /// <para>
    /// <c>https://user:pass@host/</c> is a valid URL and a way to smuggle a secret into every proxy
    /// log between here and the provider.
    /// </para>
    /// </summary>
    [Fact]
    public void A_url_carrying_credentials_is_refused()
    {
        Gateway("oauth.provider.test")
            .IsAllowed(new Uri("https://user:secret@oauth.provider.test/token"))
            .Should().BeFalse();
    }

    /// <summary>
    /// <b>The match is exact, not a suffix.</b>
    /// <para>
    /// <c>EndsWith("provider.test")</c> admits <c>evilprovider.test</c>, which is a domain an
    /// attacker can register. A subdomain is admitted only by a leading-dot entry, which is a
    /// deliberate decision rather than an accident of string comparison.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("https://eviloauth.provider.test/token", false)]
    [InlineData("https://oauth.provider.test.attacker.example/token", false)]
    [InlineData("https://oauth.provider.test/token", true)]
    public void The_host_match_is_exact(string url, bool allowed)
    {
        Gateway("oauth.provider.test").IsAllowed(new Uri(url)).Should().Be(allowed);
    }

    /// <summary>A leading dot admits the domain and anything under it, and nothing beside it.</summary>
    [Theory]
    [InlineData("https://www.googleapis.com/calendar/v3", true)]
    [InlineData("https://googleapis.com/calendar/v3", true)]
    [InlineData("https://googleapis.com.attacker.example/x", false)]
    [InlineData("https://notgoogleapis.com/x", false)]
    public void A_leading_dot_admits_a_domain_and_its_subdomains(string url, bool allowed)
    {
        Gateway(".googleapis.com").IsAllowed(new Uri(url)).Should().Be(allowed);
    }

    /// <summary>
    /// A trailing dot cannot be used to slip past the comparison.
    /// <para>
    /// <c>host.</c> and <c>host</c> resolve identically and compare differently, which is exactly
    /// the kind of thing an allow-list is bypassed with.
    /// </para>
    /// </summary>
    [Fact]
    public void A_trailing_dot_does_not_bypass_the_allow_list()
    {
        Gateway("oauth.provider.test")
            .IsAllowed(new Uri("https://oauth.provider.test./token"))
            .Should().BeTrue("a trailing dot names the same host and must be treated as the same");
    }

    /// <summary>
    /// A deployment with an empty allow-list makes no outbound call at all.
    /// <para>
    /// The correct default for a deployment that has not thought about it. The alternative default —
    /// allow anything — is the vulnerability.
    /// </para>
    /// </summary>
    [Fact]
    public void An_empty_allow_list_permits_nothing()
    {
        Gateway().IsAllowed(new Uri("https://oauth.provider.test/token")).Should().BeFalse();
    }

    /// <summary>A refused destination never reaches the provider, and says so.</summary>
    [Fact]
    public async Task A_refused_destination_is_never_reached()
    {
        var response = await Gateway("oauth.provider.test").SendAsync(new IntegrationHttpRequest
        {
            Dependency = "integrations.calendar.google",
            Method = HttpMethod.Get,
            Uri = new Uri("https://169.254.169.254/latest/meta-data/"),
            IsIdempotent = true
        });

        response.Reached.Should().BeFalse();
        response.StatusCode.Should().BeNull();
        response.IsSuccess.Should().BeFalse();
        response.TransportError.Should().Contain("allow-list");
    }

    // ── Not configured ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The default OAuth provider fabricates nothing.</b>
    /// <para>
    /// Every input needed to invent a plausible connection is available to it — a provider key, a
    /// scope list, a member. It returns <c>NotConfigured</c> from every method that could produce
    /// one, which is what §49 requires and what a fabricated grant would look identical to at the
    /// wire.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_default_oauth_provider_never_fabricates_a_grant()
    {
        var provider = new NotConfiguredOAuthProvider();

        provider.IsConfigured.Should().BeFalse();
        provider.NarrowScopes(["calendar.read", "calendar.write"]).Should().BeEmpty(
            "echoing the request would show a member scopes nobody granted");

        provider.BuildAuthorizationUri(
                new OAuthAuthorizationParameters("https://app.test/cb", "state", "challenge", []))
            .FailureKind.Should().Be(IntegrationFailureKind.NotConfigured);

        (await provider.ExchangeAsync(new OAuthExchangeRequest("code", "verifier", "https://app.test/cb")))
            .FailureKind.Should().Be(IntegrationFailureKind.NotConfigured);

        (await provider.RefreshAsync("refresh")).FailureKind
            .Should().Be(IntegrationFailureKind.NotConfigured);
    }

    /// <summary>
    /// Revoking against an unconfigured provider succeeds and reports that nothing was revoked.
    /// <para>
    /// The distinction matters at exactly one call site. A member disconnecting an integration this
    /// deployment cannot reach has still disconnected; the <c>false</c> is what tells them the
    /// provider side was not touched rather than pretending it was.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Revoking_against_an_unconfigured_provider_reports_nothing_was_revoked()
    {
        var outcome = await new NotConfiguredOAuthProvider()
            .RevokeAsync(new OAuthRevocationRequest("access", "refresh"));

        outcome.Succeeded.Should().BeTrue();
        outcome.Value.Should().BeFalse();
    }

    /// <summary>
    /// <b>The default calendar provider never claims to have written anything.</b>
    /// <para>
    /// The sharper temptation of the two: it could return a plausible external id and a timestamp,
    /// the platform would record a link, and the member would be told their event is in their
    /// calendar. It is not, and nothing downstream could ever discover that.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_default_calendar_provider_never_claims_a_write()
    {
        var provider = new NotConfiguredCalendarProvider();

        provider.IsConfigured.Should().BeFalse();
        provider.Capabilities.Should().BeEmpty(
            "a client reading the capability report must get the same answer as one calling the route");

        var draft = new ExternalCalendarEventDraft
        {
            Title = "Community meeting",
            StartsAt = DateTimeOffset.UnixEpoch,
            EndsAt = DateTimeOffset.UnixEpoch.AddHours(1),
            TimeZone = "Africa/Lagos",
            IsAllDay = false
        };

        (await provider.ListCalendarsAsync("token")).FailureKind
            .Should().Be(IntegrationFailureKind.NotConfigured);

        (await provider.CreateEventAsync("token", "primary", draft)).FailureKind
            .Should().Be(IntegrationFailureKind.NotConfigured);

        (await provider.UpdateEventAsync("token", "primary", "external", draft)).FailureKind
            .Should().Be(IntegrationFailureKind.NotConfigured);

        (await provider.DeleteEventAsync("token", "primary", "external")).FailureKind
            .Should().Be(IntegrationFailureKind.NotConfigured);

        (await provider.FindEventAsync("token", "primary", "external")).FailureKind
            .Should().Be(IntegrationFailureKind.NotConfigured);
    }

    /// <summary>
    /// An unconfigured adapter is treated as absent, not as one that fails every call.
    /// <para>
    /// An adapter assembly shipped without its credentials is the commonest deployment mistake on
    /// this surface, and the honest reading is "this deployment cannot do it".
    /// </para>
    /// </summary>
    [Fact]
    public void An_unconfigured_adapter_is_treated_as_absent()
    {
        var registry = new IntegrationProviderRegistry(
            [new NotConfiguredOAuthProvider()],
            [new NotConfiguredCalendarProvider()],
            []);

        registry.FindOAuth("none").Should().BeNull();
        registry.FindCalendar("none").Should().BeNull();
        registry.ConfiguredOAuthProviders.Should().BeEmpty();
        registry.ConfiguredCalendarProviders.Should().BeEmpty();
    }

    /// <summary>
    /// Resolution is exact and never falls back.
    /// <para>
    /// A request for <c>calendar.google</c> that found no adapter must not be served by
    /// <c>calendar.apple</c>, and must not be served by "the only calendar adapter registered".
    /// Either would mean a member's Google authorization silently wrote into a different vendor's
    /// calendar.
    /// </para>
    /// </summary>
    [Fact]
    public void Provider_resolution_is_exact_and_never_falls_back()
    {
        var registry = new IntegrationProviderRegistry(
            [], [new StubCalendarProvider("calendar.apple")], []);

        registry.FindCalendar("calendar.apple").Should().NotBeNull();
        registry.FindCalendar("calendar.google").Should().BeNull();
        registry.FindCalendar("apple").Should().BeNull();
    }

    /// <summary>
    /// A failure outcome cannot be mistaken for a success, and carrying it preserves every detail.
    /// </summary>
    [Fact]
    public void A_failure_outcome_carries_across_value_types_without_becoming_a_success()
    {
        var failure = IntegrationOutcome<string>.Failure(
            IntegrationFailureKind.RateLimited,
            "Slow down.",
            TimeSpan.FromSeconds(30),
            "quota_exceeded");

        failure.Succeeded.Should().BeFalse();
        failure.RetryAfter.Should().Be(TimeSpan.FromSeconds(30));

        var carried = failure.CarryFailure<int>();

        carried.Succeeded.Should().BeFalse();
        carried.FailureKind.Should().Be(IntegrationFailureKind.RateLimited);
        carried.RetryAfter.Should().Be(TimeSpan.FromSeconds(30));
        carried.ProviderCode.Should().Be("quota_exceeded");
    }

    /// <summary>
    /// A retry delay is only meaningful on a rate limit, so it is dropped everywhere else.
    /// <para>
    /// A <c>Retry-After</c> on an <c>Unauthorized</c> would tell a client to try again in thirty
    /// seconds for a grant that will still be revoked.
    /// </para>
    /// </summary>
    [Fact]
    public void A_retry_delay_is_kept_only_for_a_rate_limit()
    {
        IntegrationOutcome<string>
            .Failure(IntegrationFailureKind.Unauthorized, "Gone.", TimeSpan.FromSeconds(30))
            .RetryAfter.Should().BeNull();
    }

    /// <summary>
    /// <b>The provider's own retry delay reaches the member.</b>
    /// <para>
    /// §21 requires <c>Retry-After</c> to be respected rather than guessed at. The gateway captures
    /// it and the error mapping is the last place it could be dropped — and a delay dropped here
    /// sends the client back to inventing a countdown, which is exactly what the register objects
    /// to on the OTP surface for the same reason.
    /// </para>
    /// </summary>
    [Fact]
    public void A_rate_limit_carries_the_providers_own_delay_to_the_member()
    {
        var error = IntegrationErrors.FromProvider(
            IntegrationOutcome<string>.Failure(
                IntegrationFailureKind.RateLimited,
                "That provider is asking for fewer calls.",
                TimeSpan.FromSeconds(30.4)),
            "listing your calendars");

        error.Code.Should().Be(IntegrationsErrorCodes.ProviderRateLimited);

        // Rounded up, because a member reads it.
        error.Description.Should().Contain("31 seconds");
    }

    /// <summary>
    /// <b>No delay is invented when the provider gave none.</b>
    /// <para>
    /// A guessed countdown is worse than none, because a client renders it as a promise.
    /// </para>
    /// </summary>
    [Fact]
    public void A_rate_limit_without_a_provider_delay_invents_none()
    {
        var error = IntegrationErrors.FromProvider(
            IntegrationOutcome<string>.Failure(
                IntegrationFailureKind.RateLimited, "That provider is asking for fewer calls."),
            "listing your calendars");

        error.Description.Should().NotContain("Try again in");
    }

    /// <summary>One second is singular, because the sentence is read by a person.</summary>
    [Fact]
    public void A_one_second_delay_reads_as_one_second()
    {
        IntegrationErrors.FromProvider(
                IntegrationOutcome<string>.Failure(
                    IntegrationFailureKind.RateLimited, "Slow down.", TimeSpan.FromSeconds(1)),
                "listing your calendars")
            .Description.Should().Contain("1 second.").And.NotContain("1 seconds");
    }

    // ── Fixtures ────────────────────────────────────────────────────────────────────────────

    private static IntegrationHttpGateway Gateway(params string[] allowedHosts)
    {
        var options = new IntegrationsOptions();

        foreach (var host in allowedHosts)
        {
            options.AllowedOutboundHosts.Add(host);
        }

        return new IntegrationHttpGateway(
            new StubHttpClientFactory(),
            new PassThroughResilienceExecutor(),
            new StaticOptionsMonitor<IntegrationsOptions>(options),
            NullLogger<IntegrationHttpGateway>.Instance);
    }

    private sealed class StubCalendarProvider(string key) : IExternalCalendarProvider
    {
        public string Key { get; } = key;

        public bool IsConfigured => true;

        public IReadOnlyCollection<ExternalCalendarCapability> Capabilities =>
            [ExternalCalendarCapability.List];

        public Task<IntegrationOutcome<IReadOnlyList<ExternalCalendar>>> ListCalendarsAsync(
            string accessToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(IntegrationOutcome<IReadOnlyList<ExternalCalendar>>.Success([]));

        public Task<IntegrationOutcome<ExternalCalendarEvent>> CreateEventAsync(
            string accessToken,
            string calendarId,
            ExternalCalendarEventDraft draft,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(IntegrationOutcome<ExternalCalendarEvent>.Unsupported("writing"));

        public Task<IntegrationOutcome<ExternalCalendarEvent>> UpdateEventAsync(
            string accessToken,
            string calendarId,
            string externalEventId,
            ExternalCalendarEventDraft draft,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(IntegrationOutcome<ExternalCalendarEvent>.Unsupported("writing"));

        public Task<IntegrationOutcome<IntegrationVoid>> DeleteEventAsync(
            string accessToken,
            string calendarId,
            string externalEventId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(IntegrationOutcome<IntegrationVoid>.Unsupported("writing"));

        public Task<IntegrationOutcome<ExternalCalendarEvent?>> FindEventAsync(
            string accessToken,
            string calendarId,
            string externalEventId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(IntegrationOutcome<ExternalCalendarEvent?>.Unsupported("reading"));
    }

    /// <summary>Never used: every case here is refused before a client is resolved.</summary>
    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            throw new InvalidOperationException(
                "A refused destination must never reach the HTTP client.");
    }

    /// <summary>Runs the operation once. B17's own executor is tested by B17's own suite.</summary>
    private sealed class PassThroughResilienceExecutor : IResilienceExecutor
    {
        public Task<TResult> ExecuteAsync<TResult>(
            string dependency,
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default) =>
            operation(cancellationToken);

        public Task ExecuteAsync(
            string dependency,
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default) =>
            operation(cancellationToken);
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
