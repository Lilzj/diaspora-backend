namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>
/// What a client renders for one dated instance of a series.
/// <para>
/// <b>Derived, never stored.</b> An occurrence is a <c>CommunityEvent</c>, and its lifecycle is
/// already carried by <see cref="EventStatus"/>; a second stored status would be a second thing to
/// keep true and the two would disagree the first time a sweep moved one and not the other. This
/// enum is a projection computed by <c>OccurrenceStatusProjection</c> — the same discipline the
/// module already applies to "open for RSVP", which is a predicate rather than a state.
/// </para>
/// <para>
/// <see cref="Rescheduled"/> is the one value with no counterpart in <see cref="EventStatus"/>: it
/// means the occurrence is still going ahead but no longer on the date the rule generated, which is
/// exactly the case a client has to render differently and the reason the event keeps its original
/// start.
/// </para>
/// </summary>
public enum OccurrenceStatus
{
    /// <summary>On the date the rule generated, and going ahead.</summary>
    Scheduled = 0,

    /// <summary>Going ahead, but moved. The generated date travels as the original start.</summary>
    Rescheduled = 1,

    /// <summary>Called off. Produces an exception on the rule so it is never regenerated.</summary>
    Cancelled = 2,

    /// <summary>Over.</summary>
    Completed = 3
}
