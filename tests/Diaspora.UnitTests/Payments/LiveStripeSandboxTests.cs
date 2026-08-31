using System.Net;
using Diaspora.Modules.Financial.Extensibility.Payouts;
using Diaspora.Payments.Stripe;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Payments;

/// <summary>
/// The adapter against a real Stripe account.
/// <para>
/// Skipped unless <c>STRIPE_SECRET_KEY</c> is in the environment, so it is inert in CI and on any
/// machine without credentials. Run it deliberately:
/// </para>
/// <code>
/// STRIPE_SECRET_KEY=sk_test_... dotnet test --filter FullyQualifiedName~LiveStripeSandboxTests
/// </code>
/// <para>
/// <b>Nothing here moves money.</b> A balance read proves the credential and the transport; a
/// connected-account read proves the classification against a real payload. Creating a transfer
/// would need a funded platform balance and a connected account, and a test that moved money as a
/// side effect of running the suite is not a test anybody should be able to run by accident.
/// </para>
/// <para>
/// <b>Why this exists when the stubbed tests already pass.</b> Those prove the adapter builds the
/// request it means to and reads the reply it expects. They cannot tell you the key is valid, that
/// the pinned API version is still accepted, or that Stripe's account payload still has the shape
/// the mapper reads. Those are the things that break without any code changing.
/// </para>
/// </summary>
public sealed class LiveStripeSandboxTests
{
    /// <summary>
    /// The credential works and the pinned API version is still accepted. A 401 here means the key
    /// is wrong or rolled; a 400 naming the version means the pin needs raising.
    /// </summary>
    [LiveStripe]
    public async Task The_configured_key_authenticates_and_the_pinned_api_version_is_accepted()
    {
        var settings = Settings();

        using var client = new HttpClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"{settings.ApiBase.TrimEnd('/')}/v1/balance");

        StripeSettlementProvider.Authorize(request, settings);

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "the key should authenticate and the pinned Stripe-Version should still be served");
    }

    /// <summary>
    /// A guard rather than an assertion about Stripe: a live <em>secret</em> key in this test would
    /// mean somebody pointed the suite at production, where the next test to be written might not
    /// be as careful as this one.
    /// </summary>
    [LiveStripe]
    public void The_live_suite_refuses_to_run_against_a_production_key()
    {
        Settings().IsTestMode.Should().BeTrue(
            "STRIPE_SECRET_KEY must be a test key; point this suite at a sandbox account");
    }

    /// <summary>
    /// The classification, against a real account payload rather than a fixture. Needs
    /// <c>STRIPE_ACCOUNT</c> as well — a connected account id (<c>acct_…</c>) from the same sandbox.
    /// </summary>
    [LiveStripeAccount]
    public async Task A_real_connected_account_is_classified()
    {
        var settings = Settings();
        var accountId = Environment.GetEnvironmentVariable("STRIPE_ACCOUNT")!;

        var source = new StripeConnectVerificationSource(
            new StaticOptions(settings),
            new FixedAccountResolver(accountId),
            new HttpClient(),
            NullLogger<StripeConnectVerificationSource>.Instance);

        var verification = await source.GetAsync(Guid.NewGuid());

        verification.Provider.Should().Be(settings.Provider);

        // Which status is correct depends on the sandbox account, so this asserts only that the
        // payload was read rather than guessed at: an unparsed or unauthenticated response comes
        // back as NotStarted with no reference at all.
        verification.Reference.Should().Be(accountId);

        verification.Status.Should().BeOneOf(
            PayoutProviderKycStatus.Verified,
            PayoutProviderKycStatus.Pending,
            PayoutProviderKycStatus.NotStarted,
            PayoutProviderKycStatus.Restricted,
            PayoutProviderKycStatus.Rejected,
            PayoutProviderKycStatus.Expired);
    }

    private static StripeOptions Settings() => new()
    {
        SecretKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? string.Empty
    };

    private sealed class StaticOptions(StripeOptions value) : IOptionsMonitor<StripeOptions>
    {
        public StripeOptions CurrentValue => value;

        public StripeOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<StripeOptions, string?> listener) => null;
    }

    private sealed class FixedAccountResolver(string accountId) : IStripeAccountResolver
    {
        public Task<string?> ResolveAsync(
            Guid payeeUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(accountId);
    }
}

/// <summary>Runs only when <c>STRIPE_SECRET_KEY</c> is set.</summary>
public sealed class LiveStripeAttribute : FactAttribute
{
    public LiveStripeAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY")))
        {
            Skip = "Set STRIPE_SECRET_KEY to run this against a live Stripe sandbox account.";
        }
    }
}

/// <summary>Runs only when a connected account is named as well.</summary>
public sealed class LiveStripeAccountAttribute : FactAttribute
{
    public LiveStripeAccountAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STRIPE_ACCOUNT")))
        {
            Skip = "Set STRIPE_SECRET_KEY and STRIPE_ACCOUNT (acct_...) to run this.";
        }
    }
}
