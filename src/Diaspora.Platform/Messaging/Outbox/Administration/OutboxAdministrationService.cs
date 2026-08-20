using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Diaspora.Platform.Messaging.Outbox.Model;
using Diaspora.Platform.Messaging.Outbox.Options;
using Diaspora.Platform.Messaging.Outbox.Statistics;
using Diaspora.Platform.Messaging.Outbox.Telemetry;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Platform.Messaging.Outbox.Administration;

/// <summary>
/// The operator-facing surface, composed from the narrower ports.
/// <para>
/// Everything here is audited at information level with the acting principal's correlation
/// identifier. These are the operations that resurrect or abandon committed business events,
/// and "who replayed four thousand messages at 02:40" is a question that gets asked afterwards,
/// every time.
/// </para>
/// </summary>
public sealed class OutboxAdministrationService : IOutboxAdministration
{
    /// <summary>
    /// Hard ceiling on any listing or bulk replay, regardless of what the caller asks for.
    /// <para>
    /// An unbounded query against a backed-up outbox is a way to turn a delivery incident into
    /// a database incident, and the person most likely to run it is the one already under
    /// pressure. The cap is not configurable for that reason.
    /// </para>
    /// </summary>
    public const int MaxLimit = 500;

    private readonly IOutboxQueries _queries;
    private readonly IOutboxMaintenance _maintenance;
    private readonly IOutboxStore _store;
    private readonly IClock _clock;
    private readonly ICorrelationContext _correlation;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxAdministrationService> _logger;

    public OutboxAdministrationService(
        IOutboxQueries queries,
        IOutboxMaintenance maintenance,
        IOutboxStore store,
        IClock clock,
        ICorrelationContext correlation,
        IOptions<OutboxOptions> options,
        ILogger<OutboxAdministrationService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _queries = queries;
        _maintenance = maintenance;
        _store = store;
        _clock = clock;
        _correlation = correlation;
        _options = options.Value;
        _logger = logger;
    }

    public Task<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default) =>
        _queries.GetStatisticsAsync(_clock.UtcNow, cancellationToken);

    public Task<IReadOnlyList<OutboxMessage>> ListPendingAsync(
        OutboxQuery query, CancellationToken cancellationToken = default) =>
        _queries.ListPendingAsync(Bound(query), cancellationToken);

    public Task<IReadOnlyList<OutboxDeadLetter>> ListDeadLettersAsync(
        OutboxQuery query, CancellationToken cancellationToken = default) =>
        _queries.ListDeadLettersAsync(Bound(query), cancellationToken);

    public Task<IReadOnlyList<OutboxDeliveryAttempt>> ListAttemptsAsync(
        Guid messageId, int limit, CancellationToken cancellationToken = default) =>
        _queries.ListAttemptsAsync(messageId, Math.Clamp(limit, 1, MaxLimit), cancellationToken);

    public async Task<OutboxOperationResult> ReplayDeadLetterAsync(
        long deadLetterId, CancellationToken cancellationToken = default)
    {
        var replayed = await _maintenance.ReplayDeadLetterAsync(
            deadLetterId, _clock.UtcNow, cancellationToken);

        if (!replayed)
        {
            return OutboxOperationResult.None(
                $"No dead letter with identifier {deadLetterId} was found.");
        }

        OutboxTelemetry.Replayed.Add(1);

        _logger.LogInformation(
            "Dead letter {DeadLetterId} was replayed [CorrelationId: {CorrelationId}].",
            deadLetterId, _correlation.CorrelationId);

        return new OutboxOperationResult(1, $"Dead letter {deadLetterId} was re-enqueued.");
    }

    public async Task<OutboxOperationResult> ReplayDeadLettersAsync(
        OutboxQuery query, CancellationToken cancellationToken = default)
    {
        var bounded = Bound(query);

        var replayed = await _maintenance.ReplayDeadLettersAsync(
            bounded, _clock.UtcNow, cancellationToken);

        if (replayed == 0)
        {
            return OutboxOperationResult.None("No dead letters matched the filter.");
        }

        OutboxTelemetry.Replayed.Add(replayed);

        // Information, and deliberately verbose. A bulk replay recreates the load that produced
        // the dead letters in the first place; the record of what was replayed and how much of
        // it is the first thing anyone asks for if the dependency falls over again.
        _logger.LogInformation(
            "{Count} dead letter(s) were replayed [Module: {Module}, EventType: {EventType}, " +
            "CorrelationId: {CorrelationId}]. The batch was capped at {Limit}.",
            replayed, bounded.Module ?? "any", bounded.EventType ?? "any",
            _correlation.CorrelationId, bounded.Limit);

        return new OutboxOperationResult(
            replayed,
            replayed >= bounded.Limit
                ? $"{replayed} dead letter(s) were re-enqueued, which is the batch limit — run " +
                  "again to continue."
                : $"{replayed} dead letter(s) were re-enqueued.");
    }

    public async Task<OutboxOperationResult> RetryNowAsync(
        long sequence, CancellationToken cancellationToken = default)
    {
        var retried = await _maintenance.RetryNowAsync(sequence, _clock.UtcNow, cancellationToken);

        if (!retried)
        {
            return OutboxOperationResult.None(
                $"Message {sequence} was not found, or is not waiting to be retried.");
        }

        _logger.LogInformation(
            "Outbox message {Sequence} was scheduled for immediate retry " +
            "[CorrelationId: {CorrelationId}].",
            sequence, _correlation.CorrelationId);

        return new OutboxOperationResult(1, $"Message {sequence} will be claimed on the next pass.");
    }

    public async Task<OutboxOperationResult> SkipAsync(
        long sequence, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var message = await _queries.FindMessageAsync(sequence, cancellationToken);

        if (message is null)
        {
            return OutboxOperationResult.None($"Message {sequence} was not found.");
        }

        await _store.DeadLetterAsync(
            sequence,
            OutboxDeadLetterReason.Skipped,
            $"Skipped by an operator: {reason}",
            _clock.UtcNow,
            cancellationToken);

        OutboxTelemetry.DeadLettered.Add(
            1,
            new KeyValuePair<string, object?>("event.type", message.EventType),
            new KeyValuePair<string, object?>("reason", nameof(OutboxDeadLetterReason.Skipped)));

        // Warning, not information. Skipping is the one operation here that stops a committed
        // business event from ever reaching its consumers unless somebody later replays it.
        _logger.LogWarning(
            "Outbox message {Sequence} ({EventType}, {MessageId}) was skipped by an operator: " +
            "{Reason} [CorrelationId: {CorrelationId}]. The payload is retained in the " +
            "dead-letter table and can still be replayed.",
            sequence, message.EventType, message.MessageId, reason, _correlation.CorrelationId);

        return new OutboxOperationResult(
            1, $"Message {sequence} was moved to the dead-letter table and is replayable.");
    }

    public async Task<OutboxOperationResult> PurgeDeliveredAsync(
        CancellationToken cancellationToken = default)
    {
        var retention = _options.Retention;
        var cutoff = _clock.UtcNow.AddHours(-retention.DeliveredRetentionHours);

        var purged = await _maintenance.PurgeDeliveredAsync(
            cutoff, retention.PurgeBatchSize, cancellationToken);

        if (purged > 0)
        {
            OutboxTelemetry.Purged.Add(
                purged, new KeyValuePair<string, object?>("population", "delivered"));
        }

        return new OutboxOperationResult(
            purged, $"{purged} delivered message(s) older than {cutoff:O} were removed.");
    }

    /// <summary>
    /// Applies the hard caps a caller cannot raise. A negative offset would page backwards off
    /// the start of the table, which most providers translate into an error rather than an
    /// empty page.
    /// </summary>
    private static OutboxQuery Bound(OutboxQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query with
        {
            Limit = Math.Clamp(query.Limit, 1, MaxLimit),
            Offset = Math.Max(0, query.Offset)
        };
    }
}
