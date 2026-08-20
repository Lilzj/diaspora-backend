using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.Events;

/// <summary>
/// One or more signals were written for a member.
/// <para>
/// <b>Raised once per batch rather than once per signal, and the arithmetic is the reason.</b> A
/// single home-feed impression records a view signal for every card on the screen; raising an event
/// each would put twenty rows in the outbox for one scroll, and the outbox dispatcher would spend
/// its day delivering the fact that somebody looked at a page.
/// </para>
/// <para>
/// Raised by the recording service rather than by <c>UserSignal</c> itself, because no single signal
/// is the aggregate that knows a batch happened. That is unusual for this platform and is the one
/// place in the module where an event does not come off an aggregate root.
/// </para>
/// </summary>
public sealed record SignalsRecordedDomainEvent : DomainEvent
{
    public required UserId UserId { get; init; }

    public required int SignalCount { get; init; }

    /// <summary>The distinct kinds in the batch. What a consumer decides to care about.</summary>
    public required IReadOnlyList<SignalKind> Kinds { get; init; }

    public required SignalSource Source { get; init; }

    public required DateTimeOffset RecordedAt { get; init; }
}
