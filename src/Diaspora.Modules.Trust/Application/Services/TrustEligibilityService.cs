using Diaspora.Platform.Services.Abstractions;
using Diaspora.Platform.Services.Models;

namespace Diaspora.Modules.Trust.Application.Services;

/// <summary>
/// The Trust module's implementation of the platform's <see cref="IEligibilityService"/>.
/// <para>
/// <b>The second half of a join that had been open since B1.</b> The kernel declared the port; the
/// only thing answering it was <c>DefaultEligibilityService</c>, which returned
/// <see cref="EligibilityStatus.Eligible"/> for every action, for every member, unconditionally —
/// while three feet away this module held <see cref="EligibilityEvaluator"/>, the platform's real
/// answer, complete with the rule chain, the contributed thresholds and the rate limits, and served
/// it over HTTP to the client. B3 took over <c>ITrustService</c> under ADR-002 and left this one
/// where it was, which is exactly the shape PSR-1.1 named: a phase builds its own half and the
/// platform has nothing watching the other.
/// </para>
/// <para>
/// <b>Nothing observable changes on the day this ships, and that is the point.</b> No caller exists
/// yet — the port had zero consumers, which is what let a stub stand in for two years of phases. It
/// is registered so that the next module to ask "may this member do X" is answered by the module
/// that knows, rather than by a method that says yes.
/// </para>
/// <para>
/// <b>Always a dry run.</b> The kernel's signature takes an action and nothing else, so a caller
/// cannot say whether it is about to perform the action or merely rendering a button. Consuming a
/// member's daily budget on a question that turned out to be a screen refresh is the worse error of
/// the two, so this inspects and never spends. A module that means to consume a budget takes the
/// rate limiter directly, as this module's own endpoints do.
/// </para>
/// </summary>
public sealed class TrustEligibilityService : IEligibilityService
{
    private readonly EligibilityEvaluator _evaluator;
    private readonly ICurrentUser _currentUser;

    public TrustEligibilityService(EligibilityEvaluator evaluator, ICurrentUser currentUser)
    {
        _evaluator = evaluator;
        _currentUser = currentUser;
    }

    public async Task<EligibilityResult> CheckEligibilityAsync(
        string action, CancellationToken cancellationToken = default)
    {
        // The port is about the caller, because its signature carries no subject. Nobody is not
        // eligible for anything, and saying so with a code is more use to a caller than a bare
        // false — a handler that got this back has an authentication problem, not a trust one.
        if (_currentUser.UserId is not { } userId)
        {
            return EligibilityResult.NotEligible(reasonCodes: [UnauthenticatedReasonCode]);
        }

        var decision = await _evaluator.EvaluateAsync(
            userId, action, targetCountry: null, dryRun: true, cancellationToken);

        if (decision.Allowed)
        {
            return EligibilityResult.Eligible();
        }

        var code = decision.Code.ToString();

        // Restricted and NotEligible are the same refusal with different advice attached. A health
        // restriction is imposed and lifts when the platform lifts it; everything else is a
        // threshold the member can work towards, which is why only the second carries required
        // actions. Collapsing them would lose the difference between "you cannot yet" and
        // "you may not".
        return decision.Code == Domain.EligibilityCode.HEALTH_RESTRICTED
            ? new EligibilityResult
            {
                Status = EligibilityStatus.Restricted,
                CurrentRestrictions = decision.Reason is null ? [] : [decision.Reason],
                ReasonCodes = [code]
            }
            : EligibilityResult.NotEligible(
                requiredActions: decision.UnmetRequirements,
                reasonCodes: [code]);
    }

    /// <summary>Not one of Trust's codes: this refusal is about the request, not the member.</summary>
    private const string UnauthenticatedReasonCode = "UNAUTHENTICATED";
}
