namespace Diaspora.Modules.Housing.Extensibility.Fraud;

/// <summary>
/// Implemented to raise a signal about a listing before it goes on the market.
/// <para>
/// <b>This seam exists because rental fraud is the most damaging content this platform can
/// carry.</b> An advance-fee scam takes a newcomer's deposit before they have a bank account, a
/// job, or anybody to ask — and by the time they land, the advert is gone. Detection is a moving
/// target that will always be behind the fraudsters, so it must be a place where new signals can be
/// added without touching the module: a heuristic, a shared blocklist, a third-party service, or a
/// rule somebody wrote after last month's incident.
/// </para>
/// <para>
/// <b>Providers advise; they do not decide.</b> Only a <c>Blocking</c> signal stops a publication,
/// and only when the deployment is configured to enforce signals at all. Everything else becomes a
/// note on a moderator's queue — because a heuristic that silently refuses a genuine landlord's
/// listing costs the platform the landlord, and there are far more honest landlords with unusual
/// listings than there are fraudsters.
/// </para>
/// <para>
/// <b>Contract: return quickly or return nothing.</b> A provider runs on the publication path, and
/// a slow one delays a member's listing going live. Throwing is not a failure of the publication —
/// the assessor isolates each provider, so one signal source's outage costs its own signal and not
/// the listing.
/// </para>
/// </summary>
public interface IHousingFraudSignalProvider
{
    /// <summary>What this provider calls itself. Recorded on every signal it raises.</summary>
    string Name { get; }

    /// <summary>The signals this provider sees, or none.</summary>
    Task<IReadOnlyList<HousingFraudSignal>> EvaluateAsync(
        HousingFraudEvaluationContext context, CancellationToken cancellationToken = default);
}
