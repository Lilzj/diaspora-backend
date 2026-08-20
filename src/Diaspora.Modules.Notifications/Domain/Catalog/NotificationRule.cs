using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Errors;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Modules.Notifications.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Domain.Catalog;

/// <summary>
/// Which published fact becomes which message, how urgently, and on what.
/// <para>
/// <b>This row is the difference between "routing is policy-driven" and a switch statement with a
/// comment claiming it is not.</b> The event handlers in this module extract data and nothing else;
/// every decision — which template, which category, which priority, which channels, whether it waits
/// for a digest, how long it lives — is read from here at dispatch time. Changing "an interview
/// reminder is Critical and bypasses quiet hours" is an update to a row.
/// </para>
/// <para>
/// <b>Keyed on the published contract's name.</b> <c>Diaspora.Contracts.Jobs.V1.Events.InterviewScheduledIntegrationEvent</c>
/// is the key, which means a rule cannot exist for an event that does not, and a contract rename is
/// caught by the seeder rather than by silence.
/// </para>
/// <para>
/// <b>One event may have several rules.</b> An interview being scheduled notifies the candidate and,
/// on a different template with a different urgency, the employer. <see cref="Audience"/> names which
/// participant this rule addresses; the handler supplies the identifiers and never decides who gets
/// what.
/// </para>
/// </summary>
public sealed class NotificationRule : AggregateRoot<NotificationRuleId>, IAuditableEntity
{
    private NotificationRule(
        NotificationRuleId id,
        string key,
        string eventName,
        string audience,
        string templateKey,
        string categoryKey,
        NotificationPriority priority,
        DeliveryPolicy policy,
        DateTimeOffset createdAt) : base(id)
    {
        Key = key;
        EventName = eventName;
        Audience = audience;
        TemplateKey = templateKey;
        CategoryKey = categoryKey;
        Priority = priority;
        Policy = policy;
        IsActive = true;
        CreatedAt = createdAt;
    }

    private NotificationRule() { }

    /// <summary>Stable name, unique across the catalogue. Stored on every message it produces.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>The published contract's type name. See the class summary.</summary>
    public string EventName { get; private set; } = string.Empty;

    /// <summary>Which participant this rule addresses — <c>candidate</c>, <c>employer</c>, <c>owner</c>.</summary>
    public string Audience { get; private set; } = string.Empty;

    public string TemplateKey { get; private set; } = string.Empty;

    public string CategoryKey { get; private set; } = string.Empty;

    public NotificationPriority Priority { get; private set; }

    public DeliveryPolicy Policy { get; private set; }

    /// <summary>
    /// The channels this rule proposes, as flags on the channel kinds.
    /// <para>
    /// A proposal, never a decision: the member's preferences narrow it, and only a Critical priority
    /// widens it back. Stored as a bitmask because the alternative — a child table of three rows per
    /// rule — costs a join on the hottest read in the dispatcher for no expressiveness.
    /// </para>
    /// </summary>
    public int ChannelMask { get; private set; }

    /// <summary>
    /// How long after the event the message should be sent, for a scheduled rule.
    /// <para>
    /// Negative is meaningful and is the interesting case: a viewing reminder is
    /// <c>-PT24H</c> against the appointment time the event carries, which is how "the day before"
    /// is expressed without any code knowing what a viewing is.
    /// </para>
    /// </summary>
    public TimeSpan? Offset { get; private set; }

    /// <summary>Overrides the template's retention. Null to inherit it.</summary>
    public TimeSpan? RetentionPeriod { get; private set; }

    /// <summary>
    /// How long two messages from the same source are treated as one.
    /// <para>
    /// At-least-once delivery means a handler will genuinely see the same event twice, and three
    /// modules may publish about one action. Null disables de-duplication for rules where every
    /// occurrence is genuinely news.
    /// </para>
    /// </summary>
    public TimeSpan? DeduplicationWindow { get; private set; }

    public bool IsActive { get; private set; }

    public string OwnerModule { get; private set; } = string.Empty;

    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static Result<NotificationRule> Create(
        NotificationRuleId id,
        string key,
        string eventName,
        string audience,
        string templateKey,
        string categoryKey,
        NotificationPriority priority,
        DeliveryPolicy policy,
        IEnumerable<NotificationChannelKind> channels,
        string ownerModule,
        DateTimeOffset createdAt,
        TimeSpan? offset = null,
        TimeSpan? retentionPeriod = null,
        TimeSpan? deduplicationWindow = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(channels);

        if (string.IsNullOrWhiteSpace(key)
            || string.IsNullOrWhiteSpace(eventName)
            || string.IsNullOrWhiteSpace(templateKey)
            || string.IsNullOrWhiteSpace(categoryKey))
        {
            return Result.Failure<NotificationRule>(NotificationErrors.RuleNotFound);
        }

        return Result.Success(new NotificationRule(
            id,
            key.Trim().ToLowerInvariant(),
            eventName.Trim(),
            string.IsNullOrWhiteSpace(audience) ? "recipient" : audience.Trim().ToLowerInvariant(),
            templateKey.Trim().ToLowerInvariant(),
            categoryKey.Trim().ToLowerInvariant(),
            priority,
            policy,
            createdAt)
        {
            ChannelMask = ToMask(channels),
            Offset = offset,
            RetentionPeriod = retentionPeriod,
            DeduplicationWindow = deduplicationWindow,
            OwnerModule = string.IsNullOrWhiteSpace(ownerModule) ? "Notifications" : ownerModule.Trim()
        });
    }

    /// <summary>The channels this rule proposes, unpacked.</summary>
    public IReadOnlyList<NotificationChannelKind> Channels => FromMask(ChannelMask);

    public bool Proposes(NotificationChannelKind channel) =>
        (ChannelMask & (1 << (int)channel)) != 0;

    /// <summary>
    /// Brings a seeded rule up to date with what its contributor now declares.
    /// <para>
    /// Idempotent; the seeder runs on every start. The key and the event name are identity and are
    /// never changed here — a rule that needs a different event is a different rule.
    /// </para>
    /// </summary>
    public void Refresh(
        string templateKey,
        string categoryKey,
        NotificationPriority priority,
        DeliveryPolicy policy,
        IEnumerable<NotificationChannelKind> channels,
        TimeSpan? offset,
        TimeSpan? retentionPeriod,
        TimeSpan? deduplicationWindow)
    {
        ArgumentNullException.ThrowIfNull(channels);

        TemplateKey = templateKey.Trim().ToLowerInvariant();
        CategoryKey = categoryKey.Trim().ToLowerInvariant();
        Priority = priority;
        Policy = policy;
        ChannelMask = ToMask(channels);
        Offset = offset;
        RetentionPeriod = retentionPeriod;
        DeduplicationWindow = deduplicationWindow;
        IsActive = true;

        Revision++;
    }

    /// <summary>Stops the rule producing anything. Kept so its key still resolves on old messages.</summary>
    public void Deactivate()
    {
        IsActive = false;
        Revision++;
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    private static int ToMask(IEnumerable<NotificationChannelKind> channels) =>
        channels.Aggregate(0, (mask, channel) => mask | (1 << (int)channel));

    private static List<NotificationChannelKind> FromMask(int mask) =>
        Enum.GetValues<NotificationChannelKind>()
            .Where(channel => (mask & (1 << (int)channel)) != 0)
            .ToList();
}
