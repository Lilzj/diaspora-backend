using Diaspora.Domain.Abstractions;
using Diaspora.Contracts.Conversations.V1.Events;
using Diaspora.Modules.Conversations.Domain.Conversations;
using Diaspora.Modules.Conversations.Options;
using Diaspora.Platform.Messaging;
using Diaspora.Platform.Operations.Scheduling;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Conversations.Infrastructure.Hosting;

/// <summary>
/// Publishes the two facts a conversation produces by not changing — B23.x Wave 12.
///
/// <para><b>Why this is here and not in the automation platform.</b> Wave 11 refused to build either
/// trigger and said why: "deriving inactivity would mean this module reading another module's message
/// state. Conversations owns that and must publish the fact." Inactivity and standing unread are
/// derived from <c>LastMessageAt</c>, <c>UnreadCount</c>, mute, archive and departure — five pieces of
/// state that live in one aggregate, three of which no other module has ever heard of. A consumer
/// computing this from published messages would have to hold a shadow copy of the inbox.</para>
///
/// <para><b>It publishes and stops.</b> No notification, no channel, no template, no recipient
/// decision beyond who is in the conversation. What happens next is an automation the member wrote,
/// evaluated by B23 and delivered by B13 — and if nobody wrote one, nothing happens, which is the
/// correct outcome for a fact nobody asked about.</para>
///
/// <para><b>Once per silence, and the marker is what makes that true.</b> Each publication carries
/// its own event id, so a consumer's idempotency cannot collapse two of them: the suppression has to
/// be here. Inactivity is marked against the message that started the quiet, so a reply followed by
/// another silence produces a second notice and a sweep running hourly does not. Unread is a cooldown,
/// because unread is a standing state rather than an event.</para>
///
/// <para><b>Bounded, leased and never fatal.</b> One pass reads a fixed number of rows under a B17
/// distributed lease, so two instances do not both publish; a pass that throws is logged and the next
/// tick re-reads the same durable rows. The shape is B22's maintenance worker and B23's sweeper, for
/// the reason both record: a loop containing a delay and a catch is what a hand-rolled retry looks
/// like, and a sweep is not a retry.</para>
/// </summary>
public sealed class ConversationAttentionWorker : BackgroundService
{
    /// <summary>One name, stable across instances and restarts. The point is that one instance runs.</summary>
    private const string LeaseResource = "conversations.attention.sweep";

    private readonly IServiceScopeFactory _scopes;
    private readonly IDistributedLock _locks;
    private readonly IOptions<ConversationOptions> _options;
    private readonly ILogger<ConversationAttentionWorker> _logger;

    public ConversationAttentionWorker(
        IServiceScopeFactory scopes,
        IDistributedLock locks,
        IOptions<ConversationOptions> options,
        ILogger<ConversationAttentionWorker> logger)
    {
        _scopes = scopes;
        _locks = locks;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.Enabled || !_options.Value.EnableAttentionSweep)
        {
            return;
        }

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(_options.Value.AttentionSweepIntervalSeconds));

        // A pause before the first pass. A host that has just started is still opening connections
        // and running migrations, and a sweep racing them fails once per deployment for no reason
        // anybody can act on.
        using var startup = new PeriodicTimer(TimeSpan.FromSeconds(45));

        if (!await startup.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await SweepOnceAsync(stoppingToken).ConfigureAwait(false);

            if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                return;
            }
        }
    }

    /// <summary>One cycle, with the catch that must not take the host down. Its own method so the
    /// loop above contains no catch clause at all — see the type summary.</summary>
    private async Task SweepOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await SweepAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown. Not a failure.
        }
#pragma warning disable CA1031 // A sweep that threw must not stop every later sweep.
        catch (Exception exception)
        {
            _logger.LogError(
                exception, "The conversation attention sweep failed. It will run again.");
        }
#pragma warning restore CA1031
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        await using var lease = await _locks.TryAcquireAsync(
            LeaseResource, TimeSpan.FromMinutes(10), cancellationToken);

        if (lease is null)
        {
            return;
        }

        using var scope = _scopes.CreateScope();
        var services = scope.ServiceProvider;

        await PublishInactivityAsync(services, cancellationToken);
        await PublishUnreadAsync(services, cancellationToken);
    }

    /// <summary>
    /// Threads that have gone quiet, for the people waiting on a reply.
    /// </summary>
    private async Task PublishInactivityAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var conversations = services.GetRequiredService<IConversationRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var publisher = services.GetRequiredService<IIntegrationEventPublisher>();
        var clock = services.GetRequiredService<IClock>();

        var now = clock.UtcNow;
        var options = _options.Value;
        var quietBefore = now.AddDays(-options.InactivityAfterDays);

        var quiet = await conversations.ListQuietAsync(
            quietBefore, options.AttentionSweepBatchSize, cancellationToken);

        if (quiet.Count == 0)
        {
            return;
        }

        var published = 0;

        foreach (var conversation in quiet)
        {
            var owed = conversation.ParticipantsOwedInactivityNotice(now);

            foreach (var participant in owed)
            {
                await publisher.PublishAsync(
                    new ConversationInactivityDetectedIntegrationEvent
                    {
                        ConversationId = conversation.Id.Value,
                        ParticipantUserId = participant.UserId.Value,
                        ContextType = conversation.Context.Type.Key,
                        EntityId = conversation.Context.EntityId,
                        ContextTitle = conversation.Context.EntityTitle,
                        LastMessageAt = conversation.LastMessageAt,
                        QuietHours = conversation.LastMessageAt is { } spokenAt
                            ? (int)Math.Floor((now - spokenAt).TotalHours)
                            : 0,

                        // Always false as published here: the sweep skips the member who spoke last.
                        // Carried so a consumer reads a field rather than inferring it, and so a
                        // change of policy is visible.
                        IsAwaitingReply = true
                    },
                    cancellationToken);

                conversation.NoteInactivityReported(participant.UserId, now);
                published++;
            }

            if (owed.Count > 0)
            {
                conversations.Update(conversation);
            }
        }

        if (published == 0)
        {
            return;
        }

        // One save. The marks and the outbox rows commit together — a mark without its publication
        // silences a notice nobody sent, and a publication without its mark sends it again next hour.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Reported {Count} quiet conversations across {Threads} threads.", published, quiet.Count);
    }

    /// <summary>
    /// Members with a standing backlog, one fact each.
    /// </summary>
    private async Task PublishUnreadAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var conversations = services.GetRequiredService<IConversationRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var publisher = services.GetRequiredService<IIntegrationEventPublisher>();
        var clock = services.GetRequiredService<IClock>();

        var now = clock.UtcNow;
        var options = _options.Value;

        var backlogs = await conversations.ListUnreadBacklogAsync(
            options.UnreadThreshold,
            now.AddHours(-options.UnreadAfterHours),
            now.AddHours(-options.UnreadCooldownHours),
            now,
            options.AttentionSweepBatchSize,
            cancellationToken);

        if (backlogs.Count == 0)
        {
            return;
        }

        foreach (var backlog in backlogs)
        {
            await publisher.PublishAsync(
                new ConversationUnreadThresholdReachedIntegrationEvent
                {
                    MemberUserId = backlog.MemberUserId,
                    UnreadCount = backlog.UnreadCount,
                    ConversationCount = backlog.ConversationCount,
                    OldestUnreadHours = (int)Math.Floor((now - backlog.OldestUnreadAt).TotalHours)
                },
                cancellationToken);
        }

        // The cooldown is marked on every side these members hold, not only on the thread that
        // tipped them over: the backlog is a total across their inbox, so marking one thread would
        // let the next sweep report the same total from another one.
        await conversations.MarkUnreadReportedAsync(
            [.. backlogs.Select(backlog => backlog.MemberUserId)], now, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Reported an unread backlog to {Count} members.", backlogs.Count);
    }
}
