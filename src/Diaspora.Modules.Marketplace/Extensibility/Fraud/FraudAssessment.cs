namespace Diaspora.Modules.Marketplace.Extensibility.Fraud;

/// <summary>
/// Every provider's findings about one listing, and what the module should do about them.
/// <para>
/// The decision is derived from the signals rather than voted on: any severe finding holds the
/// listing, any warning flags it, and everything else is recorded. Keeping the rule here rather
/// than in the handler means it is stated once and testable without a container.
/// </para>
/// </summary>
public sealed record FraudAssessment
{
    public required IReadOnlyList<FraudSignal> Signals { get; init; }

    /// <summary>Nothing found. What an assessment with no providers registered also looks like.</summary>
    public static FraudAssessment Clear => new() { Signals = [] };

    /// <summary>The worst finding. Null when there was none.</summary>
    public FraudSignalSeverity? Severity =>
        Signals.Count == 0 ? null : Signals.Max(signal => signal.Severity);

    /// <summary>Whether the listing should be held for review before a buyer sees it.</summary>
    public bool ShouldHold => Severity == FraudSignalSeverity.Severe;

    /// <summary>Whether the listing should go into the moderation queue while staying visible.</summary>
    public bool ShouldFlag => Severity == FraudSignalSeverity.Warning;

    /// <summary>
    /// The findings as one line for the moderation trail. Sorted worst first, because a moderator
    /// reading the first clause should be reading the reason it was held.
    /// </summary>
    public string Summarize() =>
        Signals.Count == 0
            ? string.Empty
            : string.Join(
                "; ",
                Signals.OrderByDescending(signal => signal.Severity)
                    .Select(signal => $"[{signal.Severity}] {signal.Code}: {signal.Explanation}"));
}
