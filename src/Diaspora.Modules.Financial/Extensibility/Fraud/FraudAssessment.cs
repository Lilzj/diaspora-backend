namespace Diaspora.Modules.Financial.Extensibility.Fraud;

/// <summary>
/// What a fraud assessor concluded.
/// <para>
/// <b><see cref="Reasons"/> is required on anything but an allow.</b> A refusal a member cannot be
/// told anything about, and an operator cannot audit, is indistinguishable from a bug — and a fraud
/// system nobody can explain is one nobody will trust enough to leave switched on.
/// </para>
/// </summary>
public sealed record FraudAssessment
{
    public required FraudDecision Decision { get; init; }

    /// <summary>0 to 100. Higher is riskier. Advisory; the decision is what acts.</summary>
    public int Score { get; init; }

    public IReadOnlyList<string> Reasons { get; init; } = [];

    public static FraudAssessment Allow(int score = 0) =>
        new() { Decision = FraudDecision.Allow, Score = score };

    public static FraudAssessment Review(int score, params string[] reasons) =>
        new() { Decision = FraudDecision.Review, Score = score, Reasons = reasons };

    public static FraudAssessment Refuse(int score, params string[] reasons) =>
        new() { Decision = FraudDecision.Refuse, Score = score, Reasons = reasons };
}
