using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Domain.Catalog;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Errors;
using Diaspora.Modules.Notifications.Domain.Notifications;
using Diaspora.Modules.Notifications.Domain.Policies;
using Diaspora.Modules.Notifications.Domain.Preferences;
using Diaspora.Modules.Notifications.Domain.Templates;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.Modules.Notifications.Extensibility.DeepLinks;
using Diaspora.Modules.Notifications.Extensibility.Templates;
using Diaspora.Modules.Notifications.Observability;
using Diaspora.Modules.Notifications.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Notifications.Application.Dispatch;

/// <summary>
/// Turns a published fact into messages, once.
/// <para>
/// <b>The only place a notification is created in this platform.</b> Everything upstream is a
/// handler extracting values from a contract; everything downstream is a channel moving finished
/// words. This class holds the whole decision: which rules match, whether the member has already
/// been told, what the words are in their language, and what each channel should do.
/// </para>
/// <para>
/// <b>It reads its decisions and never contains them.</b> No template key, category, priority or
/// channel appears in this file as a literal. All of them come from the rule catalogue, which is
/// reference data an operator edits. That is what makes "routing is policy-driven rather than
/// hard-coded" checkable rather than aspirational.
/// </para>
/// <para>
/// <b>Nothing here fails a consumed event.</b> A missing template, a muted category, a duplicate,
/// an unrenderable variable — every one of them is recorded and returns success, because an
/// integration event that keeps failing is an outbox row that retries until it dead-letters, and a
/// preference is not a reason to dead-letter somebody's job offer.
/// </para>
/// </summary>
public sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly INotificationRuleRepository _rules;
    private readonly INotificationCategoryRepository _categories;
    private readonly NotificationTemplateCatalog _templates;
    private readonly INotificationPreferenceRepository _preferences;
    private readonly INotificationRepository _notifications;
    private readonly ITemplateRenderer _renderer;
    private readonly IDeepLinkResolver _deepLinks;
    private readonly DeliveryPolicyResolver _policy;
    private readonly IOptions<NotificationOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        INotificationRuleRepository rules,
        INotificationCategoryRepository categories,
        NotificationTemplateCatalog templates,
        INotificationPreferenceRepository preferences,
        INotificationRepository notifications,
        ITemplateRenderer renderer,
        IDeepLinkResolver deepLinks,
        DeliveryPolicyResolver policy,
        IOptions<NotificationOptions> options,
        IGuidGenerator guids,
        IClock clock,
        ILogger<NotificationDispatcher> logger)
    {
        _rules = rules;
        _categories = categories;
        _templates = templates;
        _preferences = preferences;
        _notifications = notifications;
        _renderer = renderer;
        _deepLinks = deepLinks;
        _policy = policy;
        _options = options;
        _guids = guids;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<NotificationDispatchResult>> DispatchAsync(
        NotificationTrigger trigger, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        var rules = await MatchingRulesAsync(trigger, cancellationToken);

        if (rules.Count == 0)
        {
            // Not a failure. Most published contracts have no rule, and a platform that logged a
            // warning for each one would drown the log in facts nobody asked to be told about.
            NotificationTelemetry.TriggersWithoutRule.Add(1);

            return Result.Success(
                NotificationDispatchResult.Nothing(trigger.RecipientUserId, noRuleMatched: true));
        }

        var preference = await _preferences.FindAsync(trigger.RecipientUserId, cancellationToken);
        var recipient = NotificationRecipientContext.For(
            trigger.RecipientUserId, preference, _options);

        var created = new List<NotificationId>();
        var queued = new List<NotificationChannelKind>();
        var deduplicated = false;

        foreach (var rule in rules)
        {
            var outcome = await ApplyAsync(rule, trigger, recipient, cancellationToken);

            if (outcome.WasDeduplicated)
            {
                deduplicated = true;
                continue;
            }

            if (outcome.NotificationId is { } identifier)
            {
                created.Add(identifier);
                queued.AddRange(outcome.QueuedChannels);
            }
        }

        return Result.Success(new NotificationDispatchResult
        {
            RecipientUserId = trigger.RecipientUserId,
            CreatedNotifications = created,
            QueuedChannels = queued,
            WasDeduplicated = deduplicated
        });
    }

    /// <summary>
    /// The active rules for this event and audience.
    /// <para>
    /// Audience matching is exact and lower-cased. A rule with the audience <c>recipient</c> is the
    /// catch-all a handler uses when an event concerns exactly one person.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<NotificationRule>> MatchingRulesAsync(
        NotificationTrigger trigger, CancellationToken cancellationToken)
    {
        var all = await _rules.ListForEventAsync(trigger.EventName, cancellationToken);

        return all
            .Where(rule => rule.IsActive)
            .Where(rule => string.Equals(
                rule.Audience, trigger.Audience, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>One rule, applied end to end.</summary>
    private async Task<RuleOutcome> ApplyAsync(
        NotificationRule rule,
        NotificationTrigger trigger,
        NotificationRecipientContext recipient,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        var template = await _templates.FindPublishedAsync(rule.TemplateKey, cancellationToken);

        if (template is null)
        {
            // A rule pointing at a template nobody published. Loud, because it means a member is
            // silently not being told something the platform intended to tell them — and it is
            // fixable by pressing publish.
            _logger.LogError(
                "Rule '{RuleKey}' names template '{TemplateKey}', which is not published. Nothing was sent.",
                rule.Key,
                rule.TemplateKey);

            NotificationTelemetry.RulesWithoutTemplate.Add(1);

            return RuleOutcome.None;
        }

        var source = NotificationSource.Create(
            trigger.SourceModule, trigger.EventName, trigger.SubjectType, trigger.SubjectId);

        if (await IsDuplicateAsync(rule, trigger, source, now, cancellationToken))
        {
            NotificationTelemetry.Deduplicated.Add(1);

            return RuleOutcome.Duplicate;
        }

        var category = await _categories.FindByKeyAsync(rule.CategoryKey, cancellationToken);
        var priority = rule.Priority;

        var rendered = _renderer.Render(new TemplateRenderRequest
        {
            Template = template,
            Language = recipient.Language,
            TimeZoneId = recipient.TimeZoneId,
            Values = trigger.Values,
            Count = trigger.Count
        });

        if (rendered.IsFailure)
        {
            // A required variable the handler did not supply. The message cannot be written, and
            // failing the event would retry it forever against the same missing value.
            _logger.LogError(
                "Rule '{RuleKey}' could not render template '{TemplateKey}': {Error}. Nothing was sent.",
                rule.Key,
                rule.TemplateKey,
                rendered.Error.Code);

            NotificationTelemetry.RenderFailures.Add(1);

            return RuleOutcome.None;
        }

        var notification = Build(
            rule, trigger, recipient, template, rendered.Value, source, priority, now);

        if (notification.IsFailure)
        {
            return RuleOutcome.None;
        }

        await _notifications.AddAsync(notification.Value, cancellationToken);

        var queued = await QueueAsync(
            notification.Value, rule, category, recipient, priority, trigger, now, cancellationToken);

        NotificationTelemetry.Created.Add(
            1,
            new KeyValuePair<string, object?>("category", rule.CategoryKey),
            new KeyValuePair<string, object?>("priority", priority.ToString()));

        return new RuleOutcome
        {
            NotificationId = notification.Value.Id,
            QueuedChannels = queued
        };
    }

    /// <summary>
    /// Whether an equivalent message already reached this member recently.
    /// <para>
    /// <b>Not an optimisation — a correctness requirement.</b> At-least-once delivery guarantees a
    /// handler will see the same published event twice, and without this the member sees it twice
    /// too. The key is the source rather than the event identifier, because two <em>different</em>
    /// events about the same subject — a listing updated and then republished within a minute —
    /// should also produce one message.
    /// </para>
    /// </summary>
    private async Task<bool> IsDuplicateAsync(
        NotificationRule rule,
        NotificationTrigger trigger,
        NotificationSource source,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var window = rule.DeduplicationWindow
                     ?? TimeSpan.FromMinutes(_options.Value.DefaultDeduplicationMinutes);

        if (window <= TimeSpan.Zero)
        {
            return false;
        }

        return await _notifications.ExistsRecentlyAsync(
            trigger.RecipientUserId,
            rule.TemplateKey,
            source.DeduplicationKey,
            now - window,
            cancellationToken);
    }

    /// <summary>Assembles the aggregate from the rendered words.</summary>
    private Result<Notification> Build(
        NotificationRule rule,
        NotificationTrigger trigger,
        NotificationRecipientContext recipient,
        NotificationTemplate template,
        RenderedTemplate rendered,
        NotificationSource source,
        NotificationPriority priority,
        DateTimeOffset now)
    {
        var content = NotificationContent.Create(
            rendered.Title,
            rendered.Body,
            rendered.Language,
            rendered.Format,
            rendered.ShortBody);

        var link = DeepLink.TryCreate(rendered.Route, trigger.SubjectType, trigger.SubjectId);

        var retention = rule.RetentionPeriod
                        ?? template.RetentionPeriod
                        ?? TimeSpan.FromDays(_options.Value.DefaultRetentionDays);

        var notification = Notification.Create(
            NotificationId.From(_guids.NewSequentialGuid()),
            trigger.RecipientUserId,
            rule.CategoryKey,
            priority,
            rule.TemplateKey,
            rule.Key,
            source,
            content,
            link,

            // The fact's own moment, not the delivery's. An outbox row delivered ten minutes late
            // must not sort above messages that genuinely arrived after it.
            trigger.OccurredAt,
            retention > TimeSpan.Zero ? trigger.OccurredAt.Add(retention) : null,
            trigger.GroupKey ?? source.DeduplicationKey);

        if (notification.IsFailure)
        {
            return notification;
        }

        if (rendered.Actions.Count > 0)
        {
            notification.Value.SetActions(rendered.Actions.Select(action =>
                NotificationAction.Create(
                    _guids.NewSequentialGuid(),
                    action.Key,
                    action.Label,
                    action.Style,
                    DeepLink.TryCreate(action.Route, trigger.SubjectType, trigger.SubjectId))));
        }

        return notification;
    }

    /// <summary>
    /// Records what each channel is to do.
    /// <para>
    /// Every plan produces a row, including the suppressed ones. "We chose not to tell them, and
    /// here is why" is the answer an operator needs during a complaint, and it cannot be
    /// reconstructed later from a row that was never written.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<NotificationChannelKind>> QueueAsync(
        Notification notification,
        NotificationRule rule,
        NotificationCategory? category,
        NotificationRecipientContext recipient,
        NotificationPriority priority,
        NotificationTrigger trigger,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var scheduledFor = ResolveSchedule(rule, trigger, now);

        var plans = await _policy.ResolveAsync(
            recipient.UserId,
            rule,
            category,
            recipient.Preference,
            priority,
            scheduledFor,
            cancellationToken);

        var queued = new List<NotificationChannelKind>(plans.Count);

        foreach (var plan in plans)
        {
            switch (plan.Outcome)
            {
                case DeliveryStatus.Pending:
                case DeliveryStatus.Deferred:
                    notification.Queue(
                        _guids.NewSequentialGuid(), plan.Channel, plan.DeliverAfter, now);
                    queued.Add(plan.Channel);
                    break;

                case DeliveryStatus.Suppressed:
                    notification.Suppress(
                        _guids.NewSequentialGuid(),
                        plan.Channel,
                        plan.ReasonCode ?? SuppressionReasons.RulePolicy,
                        now);
                    break;

                case DeliveryStatus.Digested:
                    // Queued with no digest yet. The digest sweep claims it, which is what keeps a
                    // digest a summary of what happened rather than a promise made in advance.
                    notification.Queue(
                        _guids.NewSequentialGuid(), plan.Channel, plan.DeliverAfter, now);
                    notification.FoldIntoDigest(
                        _guids.NewSequentialGuid(), plan.Channel, Guid.Empty, now);
                    break;

                case DeliveryStatus.Unsupported:
                    notification.MarkChannelUnsupported(
                        _guids.NewSequentialGuid(), plan.Channel, now);
                    break;

                default:
                    break;
            }
        }

        return queued;
    }

    /// <summary>
    /// When a scheduled rule wants the message sent.
    /// <para>
    /// The offset is applied to the moment the event names — the interview time, the viewing
    /// appointment — so <c>-PT24H</c> means "the day before" without any code here knowing what a
    /// viewing is. A rule with an offset and no subject moment sends immediately rather than
    /// guessing, because guessing would put a reminder a day after something that already happened.
    /// </para>
    /// </summary>
    private static DateTimeOffset? ResolveSchedule(
        NotificationRule rule, NotificationTrigger trigger, DateTimeOffset now)
    {
        if (rule.Policy != DeliveryPolicy.Scheduled || rule.Offset is not { } offset)
        {
            return null;
        }

        if (trigger.SubjectMoment is not { } moment)
        {
            return null;
        }

        var scheduled = moment.Add(offset);

        // A reminder whose moment has already passed is sent now rather than skipped: somebody who
        // booked a viewing for tomorrow morning at midnight still deserves to be reminded.
        return scheduled <= now ? now : scheduled;
    }

    /// <summary>What applying one rule produced. Internal to this class.</summary>
    private sealed record RuleOutcome
    {
        public static readonly RuleOutcome None = new();

        public static readonly RuleOutcome Duplicate = new() { WasDeduplicated = true };

        public NotificationId? NotificationId { get; init; }

        public IReadOnlyList<NotificationChannelKind> QueuedChannels { get; init; } = [];

        public bool WasDeduplicated { get; init; }
    }
}
