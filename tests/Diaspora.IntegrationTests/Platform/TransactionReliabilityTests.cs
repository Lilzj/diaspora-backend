using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// B1.0.2 in the running host.
/// <para>
/// The unit suite proves the execution semantics against an injected retrying strategy, which is
/// the only place they can be proven — this host runs on SQLite, whose strategy does not retry, so
/// the verified path is deliberately not exercised here.
/// </para>
/// <para>
/// What this suite is for is the other half of the claim: that the deployment reports honestly how
/// its transactions execute, and that a patch to the unit of work changed nothing a request can
/// observe.
/// </para>
/// </summary>
[Collection(PlatformCollection.Name)]
public sealed class TransactionReliabilityTests : PooledIdentityTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public TransactionReliabilityTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// Readiness reports how transactions execute — as a capability rather than a provider name,
    /// which is the same thing the platform keys its own behaviour off.
    /// </summary>
    [Fact]
    public async Task Readiness_reports_how_transactions_execute()
    {
        var transactions = await HttpClient.ReadinessCheckAsync("transactions");

        transactions.GetProperty("status").GetString().Should().Be("Healthy");

        var data = transactions.GetProperty("data");

        // SQLite's strategy does not retry, so commit verification is not required and no receipts
        // are written. Reporting that plainly is what lets an operator tell the two deployments
        // apart without reading configuration.
        data.GetProperty("retriesOnFailure").GetBoolean().Should().BeFalse();
        data.GetProperty("commitVerification").GetString().Should().Be("not required");
    }

    /// <summary>
    /// The regression that matters most: an ordinary write still commits, still publishes, and
    /// still reads back. B1.0.2 rewrote the method every one of those goes through.
    /// </summary>
    [Fact]
    public async Task An_ordinary_write_still_commits_and_still_produces_its_events()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/community/posts",
            new { body = "Committed under the patched unit of work.", visibility = "public", publish = true },
            JsonOptions);

        response.IsSuccessStatusCode.Should().BeTrue();

        (await Factory.PendingOutboxAsync()).Should().NotBeEmpty(
            "the aggregate and its outbox row still commit together");

        await Factory.DrainOutboxAsync();

        (await Factory.DeadLettersAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// No receipts on a provider that does not retry. The verification machinery costs a write per
    /// transaction, and a deployment that can never use it should not pay for it.
    /// </summary>
    [Fact]
    public async Task A_non_retrying_provider_writes_no_verification_receipts()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await HttpClient.PostAsJsonAsync(
            "/api/v1/community/posts",
            new { body = "No receipt for this.", visibility = "public", publish = true },
            JsonOptions);

        var transactions = await HttpClient.ReadinessCheckAsync("transactions");

        transactions.GetProperty("data").TryGetProperty("retainedReceipts", out _)
            .Should().BeFalse("nothing is written, so there is nothing to count");
    }
}
