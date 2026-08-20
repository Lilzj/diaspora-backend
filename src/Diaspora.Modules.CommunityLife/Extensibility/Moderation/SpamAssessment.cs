using Diaspora.Modules.CommunityLife.Domain.Enums;

namespace Diaspora.Modules.CommunityLife.Extensibility.Moderation;

/// <summary>
/// What every registered spam-signal provider said, taken together.
/// <para>
/// <b>The composition rule is deliberately conservative: one severe signal holds publication, two
/// warnings flag, and anything else is a note.</b> The alternative — a threshold on a summed score —
/// would let three unrelated weak signals block a real community event, and the cost of a false
/// positive here is a church unable to announce a service.
/// </para>
/// </summary>
public sealed record SpamAssessment
{
    public IReadOnlyList<EventSpamSignal> Signals { get; init; } = [];

    public static SpamAssessment Clean { get; } = new();

    /// <summary>Whether publication should be held for a moderator.</summary>
    public bool ShouldHold =>
        Signals.Any(signal => signal.Severity == EventSpamSignalSeverity.Severe);

    /// <summary>Whether the event should be flagged but still published.</summary>
    public bool ShouldFlag =>
        !ShouldHold
        && Signals.Count(signal => signal.Severity == EventSpamSignalSeverity.Warning) >= 2;

    /// <summary>Whether there is anything worth writing into the moderation trail.</summary>
    public bool HasSignals => Signals.Count > 0;

    /// <summary>The trail entry. One line per signal, source first.</summary>
    public string Summarise() =>
        string.Join(
            "; ",
            Signals.Select(signal =>
                $"[{signal.Severity}] {signal.Source ?? "unknown"}: {signal.Reason}"));
}
