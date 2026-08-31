using Diaspora.Modules.Financial.Extensibility.Payouts;

namespace Diaspora.Modules.Financial.Infrastructure.Payouts;

/// <summary>
/// Routes a payout and then checks the payee actually stands with the provider that would carry it.
/// <para>
/// <b>Two questions, deliberately answered by two components.</b> <c>PayoutProviderRouter</c> is
/// pure and synchronous: given a matrix, can this corridor be served at all. This resolver asks the
/// second question, which needs a payee and a network call — has that person completed this
/// provider's own onboarding. Folding the second into the first would make every routing test need
/// a fake verification source, and would put an I/O call inside a component whose whole value is
/// being trivially testable.
/// </para>
/// <para>
/// <b>This is the one place an alternate route may legitimately be taken.</b> Nothing has been
/// submitted yet, so choosing a different provider costs nothing and risks nothing — which is
/// exactly the condition <c>PayoutRoutingResult.Alternates</c> documents. If the preferred provider
/// needs onboarding the payee has not done and a second provider does not, the second one carries
/// it. After submission this is never true again: a provider that timed out may already have moved
/// the money, and the next step there is reconciliation, not another provider.
/// </para>
/// <para>
/// <b>A route requiring onboarding that nothing can report on is refused.</b> No verification source
/// ships — like every other provider seam here — so a deployment that marks a row
/// <c>RequiresProviderOnboarding</c> without registering something to answer it gets a refusal
/// rather than an attempt. That is the safe direction and it keeps the requirement visible.
/// </para>
/// </summary>
public sealed class PayoutRouteResolver
{
    private readonly PayoutProviderRouter _router;
    private readonly IReadOnlyList<IPayoutProviderVerificationSource> _sources;

    public PayoutRouteResolver(
        PayoutProviderRouter router, IEnumerable<IPayoutProviderVerificationSource> sources)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(sources);

        _router = router;
        _sources = [.. sources];
    }

    /// <summary>
    /// The route this payout may take, or the reason it may take none. Never throws for an
    /// unroutable payout, and never throws because a provider was unreachable.
    /// </summary>
    public async Task<PayoutRoutingResult> ResolveAsync(
        PayoutRouteRequest request,
        Guid payeeUserId,
        CancellationToken cancellationToken = default)
    {
        var routed = _router.Resolve(request);

        if (!routed.IsRouted)
        {
            return routed;
        }

        // Preference order, chosen first. Every one of these already matched country, rail,
        // currency, amount and the platform's own verification.
        List<PayoutCapability> candidates = [routed.Capability!, .. routed.Alternates];

        if (!candidates.Any(candidate => candidate.RequiresProviderOnboarding))
        {
            return routed;
        }

        var outstanding = new List<string>();

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];

            if (!candidate.RequiresProviderOnboarding)
            {
                return Choose(candidates, index);
            }

            var verification = await VerifyAsync(candidate.Provider, payeeUserId, cancellationToken);

            if (verification.IsPayable)
            {
                return Choose(candidates, index);
            }

            outstanding.Add(Describe(candidate.Provider, verification));
        }

        return PayoutRoutingResult.Refused(
            PayoutRefusalReason.ProviderOnboardingRequired, string.Join("; ", outstanding));
    }

    /// <summary>
    /// Takes the candidate at <paramref name="index"/> and offers the rest that need no onboarding.
    /// <para>
    /// A later candidate that <em>does</em> require onboarding is left out rather than listed
    /// optimistically: it has not been checked, and an alternate nobody verified is a route a caller
    /// could take and be refused by. Checking every one of them would mean a provider API call per
    /// alternate on every payout, to populate a list whose first entry is almost always the answer.
    /// </para>
    /// </summary>
    private static PayoutRoutingResult Choose(List<PayoutCapability> candidates, int index) =>
        PayoutRoutingResult.Routed(
            candidates[index],
            [.. candidates
                .Where((candidate, position) =>
                    position != index && !candidate.RequiresProviderOnboarding)]);

    /// <summary>
    /// Asks the source for a provider, or reports the payee as not started when there is none.
    /// <para>
    /// A source that throws is treated the same way. "We could not ask" and "they are not onboarded"
    /// both mean do not pay, and an exception escaping here would take down a sweep that is
    /// processing other people's money.
    /// </para>
    /// </summary>
    private async Task<PayoutProviderVerification> VerifyAsync(
        string provider, Guid payeeUserId, CancellationToken cancellationToken)
    {
        var source = _sources.FirstOrDefault(candidate =>
            string.Equals(candidate.Provider, provider, StringComparison.OrdinalIgnoreCase));

        if (source is null)
        {
            return PayoutProviderVerification.Unknown(provider);
        }

        try
        {
            return await source.GetAsync(payeeUserId, cancellationToken)
                ?? PayoutProviderVerification.Unknown(provider);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception)
#pragma warning restore CA1031
        {
            return PayoutProviderVerification.Unknown(provider);
        }
    }

    private static string Describe(string provider, PayoutProviderVerification verification) =>
        verification.OutstandingRequirements.Count == 0
            ? $"{provider}: {verification.Status}"
            : $"{provider}: {verification.Status} ({string.Join(", ", verification.OutstandingRequirements)})";
}
