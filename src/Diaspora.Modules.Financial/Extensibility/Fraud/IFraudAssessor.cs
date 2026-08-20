namespace Diaspora.Modules.Financial.Extensibility.Fraud;

/// <summary>
/// Looks at a payment before it is attempted.
/// <para>
/// <b>The seam a B16 fraud model lands on, with a deliberately dull default behind it today.</b> The
/// shipped assessor applies velocity limits and an unverified-member ceiling — rules a person can
/// read and an operator can tune — and refuses nothing else. Shipping a half-guessed heuristic that
/// declined real payments would be worse than shipping none, and the platform recorded exactly that
/// reasoning for B12's spam signals.
/// </para>
/// <para>
/// Additive: every registered assessor runs and the strictest decision wins. That is what lets a
/// model be added alongside the velocity rules rather than instead of them.
/// </para>
/// </summary>
public interface IFraudAssessor
{
    string Name { get; }

    Task<FraudAssessment> AssessAsync(
        FraudContext context, CancellationToken cancellationToken = default);
}
