using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Dispatch;

/// <summary>
/// What a consumed integration event yields: facts, and no decisions.
/// <para>
/// <b>This record is where "business modules never create notifications directly" is actually
/// enforced.</b> An event handler extracts identifiers and values from a published contract and
/// builds one of these. It cannot name a template, a category, a priority or a channel — none of
/// those fields exist here. The rule catalogue supplies every one of them, keyed on
/// <see cref="EventName"/> and <see cref="Audience"/>.
/// </para>
/// <para>
/// An architecture test asserts that no handler in this module references a template key or a
/// priority literal, which is what keeps the rule from quietly migrating back into code.
/// </para>
/// </summary>
public sealed record NotificationTrigger
{
    /// <summary>The published contract's type name. The rule catalogue is keyed on it.</summary>
    public required string EventName { get; init; }

    /// <summary>The module that published it. Recorded on the message as its source.</summary>
    public required string SourceModule { get; init; }

    /// <summary>
    /// Who this trigger addresses, and which rules apply.
    /// <para>
    /// One event commonly produces several triggers: an interview being scheduled addresses the
    /// candidate and the employer with different wording and different urgency. The handler builds
    /// one trigger per audience and the catalogue decides what each becomes.
    /// </para>
    /// </summary>
    public required string Audience { get; init; }

    public required UserId RecipientUserId { get; init; }

    /// <summary>What kind of record it concerns — <c>interview</c>, <c>listing</c>. Never parsed here.</summary>
    public string? SubjectType { get; init; }

    public string? SubjectId { get; init; }

    /// <summary>
    /// The values the template will substitute, keyed on declared variable names.
    /// <para>
    /// Typed as <c>object?</c> so the renderer can apply the declared format — a
    /// <c>DateTimeOffset</c> becomes the reader's wall clock, a decimal becomes money in their
    /// culture. A handler that pre-formats is opting out of localization and usually should not.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, object?> Values { get; init; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The moment a scheduled rule computes its offset against.
    /// <para>
    /// The interview time, the viewing appointment, the event start. Null for rules that send
    /// immediately, which is most of them.
    /// </para>
    /// </summary>
    public DateTimeOffset? SubjectMoment { get; init; }

    /// <summary>Chooses the plural form. Null for messages that are not about a count of things.</summary>
    public int? Count { get; init; }

    /// <summary>
    /// Groups messages that are really the same thing — three reactions to one post.
    /// Falls back to the subject when the handler states none.
    /// </summary>
    public string? GroupKey { get; init; }

    /// <summary>
    /// The moment the fact happened, from the published event.
    /// <para>
    /// Carried explicitly rather than taken from the clock, because an outbox row may be delivered
    /// minutes after the fact and a message stamped with its delivery time would sort wrongly in the
    /// member's own inbox.
    /// </para>
    /// </summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
