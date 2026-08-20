namespace Diaspora.Modules.CommunityLife.Domain.Policies;

/// <summary>
/// One dated slot a recurrence rule produces, before anything has been written.
/// <para>
/// Carries both the local date the rule generated it from and the two instants it resolves to,
/// because the two are needed for different things: the date is the rule's own vocabulary — an
/// exception is a date, not an instant — and the instants are what the event is stored and compared
/// on.
/// </para>
/// <para>
/// <b><see cref="Sequence"/> is the ordinal in the generated series and never moves.</b> Cancelling
/// the third of ten does not renumber the fourth, which is what lets "this and future" be a
/// comparison rather than a recount, and lets the occurrence page be tie-broken on a value that is
/// unique within a series.
/// </para>
/// </summary>
/// <param name="Sequence">The 1-based ordinal in the rule's generated dates.</param>
/// <param name="LocalDate">The local date in the series' own time zone.</param>
/// <param name="StartsAt">The resolved start instant.</param>
/// <param name="EndsAt">The resolved end instant.</param>
public readonly record struct RecurrenceSlot(
    int Sequence,
    DateOnly LocalDate,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt);
