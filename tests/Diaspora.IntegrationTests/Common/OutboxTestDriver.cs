using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Diaspora.Platform.Messaging.Outbox.Dispatch;
using Diaspora.Platform.Messaging.Outbox.Model;
using Diaspora.Platform.Messaging.Outbox.Statistics;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.IntegrationTests.Common;

/// <summary>
/// Drives the outbox by hand, so a test can be deterministic about delivery.
/// <para>
/// The background dispatcher is switched off in this host. A test that posts a request and then
/// waits to see whether a consumer ran is asserting on a timer, and a suite full of those either
/// sleeps for seconds it does not need or fails on a loaded build agent. Draining explicitly
/// says exactly what the test means: "now deliver everything, then tell me what the consumer
/// did".
/// </para>
/// <para>
/// The drain loops until the queue stops moving rather than running one pass, because delivery
/// cascades — a consumer that writes an aggregate raises its own events, which enqueue their own
/// messages. One pass would leave the second generation undelivered and the test asserting on
/// half a chain.
/// </para>
/// </summary>
public static class OutboxTestDriver
{
    /// <summary>
    /// Delivers everything currently queued, including anything the delivery itself produces.
    /// </summary>
    /// <returns>How many messages were published across every pass.</returns>
    /// <remarks>
    /// <b>The cap is a runaway guard, not a budget</b> — the loop already stops when a pass claims
    /// nothing. It was 20 until 21 Oct 2026, when every community join began publishing a fact:
    /// a collection whose shared database had accumulated more than twenty batches stopped short,
    /// and the message a test had just caused arrived on some later test's drain instead.
    /// </remarks>
    public static async Task<int> DrainOutboxAsync(
        this WebApplicationFactory<Api.Program> factory, int maxPasses = 500)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var published = 0;

        for (var pass = 0; pass < maxPasses; pass++)
        {
            using var scope = factory.Services.CreateScope();

            var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>();

            // Between passes, messages deferred behind a partition-mate's failure and messages
            // whose backoff has not elapsed are both invisible. Recovering leases first means a
            // pass abandoned by a previous test does not hide work from this one.
            await dispatcher.RecoverExpiredLeasesAsync();

            var result = await dispatcher.DispatchOnceAsync();

            published += result.Published;

            if (result.Claimed == 0)
            {
                break;
            }
        }

        return published;
    }

    /// <summary>Runs exactly one pass, for tests that assert on partial delivery.</summary>
    public static async Task<OutboxDispatchResult> DispatchOnceAsync(
        this WebApplicationFactory<Api.Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<IOutboxDispatcher>()
            .DispatchOnceAsync();
    }

    /// <summary>Returns expired leases to the pending pool, as the dispatcher loop would.</summary>
    public static async Task<int> RecoverOutboxLeasesAsync(
        this WebApplicationFactory<Api.Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<IOutboxDispatcher>()
            .RecoverExpiredLeasesAsync();
    }

    /// <summary>Current depth, ages and breakdowns.</summary>
    public static async Task<OutboxStatistics> OutboxStatisticsAsync(
        this WebApplicationFactory<Api.Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        using var scope = factory.Services.CreateScope();

        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        return await scope.ServiceProvider
            .GetRequiredService<IOutboxQueries>()
            .GetStatisticsAsync(clock.UtcNow);
    }

    /// <summary>Every message still awaiting delivery, oldest first.</summary>
    public static async Task<IReadOnlyList<OutboxMessage>> PendingOutboxAsync(
        this WebApplicationFactory<Api.Program> factory, string? eventType = null, int limit = 200)
    {
        ArgumentNullException.ThrowIfNull(factory);

        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<IOutboxQueries>()
            .ListPendingAsync(new OutboxQuery(EventType: eventType, Limit: limit));
    }

    /// <summary>Every dead letter currently retained.</summary>
    public static async Task<IReadOnlyList<OutboxDeadLetter>> DeadLettersAsync(
        this WebApplicationFactory<Api.Program> factory, string? eventType = null, int limit = 200)
    {
        ArgumentNullException.ThrowIfNull(factory);

        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<IOutboxQueries>()
            .ListDeadLettersAsync(new OutboxQuery(EventType: eventType, Limit: limit));
    }
}
