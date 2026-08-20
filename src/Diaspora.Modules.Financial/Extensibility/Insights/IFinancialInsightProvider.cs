namespace Diaspora.Modules.Financial.Extensibility.Insights;

/// <summary>
/// Produces observations about a member's or the platform's financial activity.
/// <para>
/// <b>Declared, unimplemented, and that is the whole of this phase's AI readiness for insights.</b>
/// The brief asks for seams and explicitly excludes implementations. Additive, so a model provider
/// runs alongside a rule-based one rather than replacing it.
/// </para>
/// </summary>
public interface IFinancialInsightProvider
{
    string Name { get; }

    Task<IReadOnlyList<FinancialInsight>> GetInsightsAsync(
        Guid userId, CancellationToken cancellationToken = default);
}
