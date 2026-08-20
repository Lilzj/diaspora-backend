using Diaspora.Modules.Housing.Domain.Enums;

namespace Diaspora.Modules.Housing.Extensibility.Fraud;

/// <summary>
/// What every provider said, taken together.
/// <para>
/// <see cref="ShouldBlock"/> is the only question the publication path asks; everything else is
/// recorded on the moderation trail so a human can read it later. That asymmetry is the module's
/// posture on automated refusal: a signal is cheap to record and expensive to act on.
/// </para>
/// </summary>
public sealed record HousingFraudAssessment
{
    public required IReadOnlyList<HousingFraudSignal> Signals { get; init; }

    /// <summary>Whether any provider raised a blocking signal.</summary>
    public bool HasBlockingSignal =>
        Signals.Any(signal => signal.Severity == HousingFraudSignalSeverity.Blocking);

    /// <summary>Whether anything at all was raised, at any severity.</summary>
    public bool HasSignals => Signals.Count > 0;

    /// <summary>
    /// Whether the listing should be held.
    /// <para>
    /// Takes the deployment's enforcement setting as an argument rather than reading configuration,
    /// so this type stays a value the tests can construct without a container.
    /// </para>
    /// </summary>
    public bool ShouldBlock(bool enforceSignals) => enforceSignals && HasBlockingSignal;

    /// <summary>
    /// A single line for the moderation trail, or null when there is nothing to say.
    /// </summary>
    public string? ToModerationNote() =>
        Signals.Count == 0
            ? null
            : string.Join(
                "; ",
                Signals.Select(signal =>
                    $"[{signal.Severity}] {signal.Code}: {signal.Description}"
                    + (signal.Source is { Length: > 0 } source ? $" ({source})" : string.Empty)));

    /// <summary>Nothing raised.</summary>
    public static HousingFraudAssessment Clear => new() { Signals = [] };
}
