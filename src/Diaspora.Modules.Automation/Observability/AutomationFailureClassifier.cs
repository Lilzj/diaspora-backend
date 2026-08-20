using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Platform.Operations.Resilience;

namespace Diaspora.Modules.Automation.Observability;

/// <summary>
/// Turns a thrown exception into the failure kind a member and an operator both read.
/// <para>
/// <b>B24-G40.</b> Every action that threw used to be recorded as
/// <see cref="AutomationFailureKind.ActionFailed"/>, whatever had actually happened. §17 names that
/// exact anti-pattern — "do not classify everything as <c>Unknown</c>" — and the consequence was not
/// only a poor diagnostic: <c>ActionFailed</c> is rendered to the member as "something went wrong",
/// so a member whose calendar provider was down and a member who hit a genuine bug were told the
/// same thing and given the same non-advice.
/// </para>
/// <para>
/// <b>Two taxonomies, deliberately, and neither is new.</b> The platform's
/// <see cref="IFailureClassifier"/> answers one question — is this worth retrying — and answers it
/// for every dependency the deployment has registered a classifier for. That is the question this
/// class cannot answer for itself and must not re-implement. What it adds is the *shape* of the
/// failure in the frontend's own vocabulary, which is
/// <see cref="AutomationFailureKind"/> — twelve values the client already renders, already
/// translates, and already offers recovery advice against.
/// </para>
/// <para>
/// <b>The exception's own type is read before the platform's verdict, not after.</b> A timeout is
/// transient <em>and</em> is specifically a timeout, and telling a member "the platform did not hear
/// back in time" is more use than telling them to try again. Only where the type says nothing does
/// the retry classification decide, and there the honest answers are narrow: transient becomes
/// <see cref="AutomationFailureKind.BackendUnavailable"/> because something the platform depends on
/// did not answer, and permanent becomes <see cref="AutomationFailureKind.ActionFailed"/> because a
/// permanent failure of unknown shape is exactly what that value has always meant.
/// </para>
/// </summary>
public sealed class AutomationFailureClassifier
{
    private readonly IFailureClassifier _platform;

    public AutomationFailureClassifier(IFailureClassifier platform) => _platform = platform;

    /// <summary>
    /// Classifies one exception.
    /// </summary>
    public AutomationFailureKind Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // An inner exception carries the shape where a wrapper carries only the fact of failure.
        // Unwrapping one level is enough for the cases that matter — an HttpRequestException inside
        // an InvalidOperationException — and unwrapping indefinitely would classify a failure by
        // whatever happened deepest, which is often the least relevant thing about it.
        var kind = FromType(exception) ?? (exception.InnerException is { } inner
            ? FromType(inner)
            : null);

        if (kind is { } recognised)
        {
            return recognised;
        }

        return _platform.Classify(exception) switch
        {
            // A cancelled action is the host stopping, not the rule being wrong. Recorded as a
            // timeout because that is the value whose member-facing wording — "the platform did not
            // hear back about this in time" — is true of it, and because inventing a thirteenth
            // failure kind would change a union the client compiles against.
            FailureClassification.Cancelled => AutomationFailureKind.Timeout,

            // Something the platform depends on did not answer. Distinguishable from a rule that is
            // wrong, which is the distinction a member needs in order to know whether to change
            // anything.
            FailureClassification.Transient => AutomationFailureKind.BackendUnavailable,

            _ => AutomationFailureKind.ActionFailed
        };
    }

    /// <summary>
    /// What the exception's own type says, where it says anything.
    /// <para>
    /// Deliberately short. Every entry is a type whose meaning is unambiguous; a longer list would
    /// start guessing, and a guessed classification is worse than <c>ActionFailed</c> because it
    /// reads as knowledge.
    /// </para>
    /// </summary>
    private static AutomationFailureKind? FromType(Exception exception) => exception switch
    {
        TimeoutException => AutomationFailureKind.Timeout,

        // The breaker refused the call: the dependency was not reached, and the platform already
        // knows it is unwell. Reported as the provider being unavailable rather than as this rule
        // failing, because retrying this rule is not what fixes it.
        CircuitOpenException => AutomationFailureKind.ProviderUnavailable,

        // The bulkhead was full. The platform, not the provider — this deployment shed the work.
        BulkheadFullException => AutomationFailureKind.BackendUnavailable,

        HttpRequestException http => http.StatusCode switch
        {
            System.Net.HttpStatusCode.TooManyRequests => AutomationFailureKind.RateLimited,
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                AutomationFailureKind.IntegrationExpired,
            System.Net.HttpStatusCode.RequestTimeout => AutomationFailureKind.Timeout,
            _ => AutomationFailureKind.ProviderUnavailable
        },

        // Nothing maps to InvalidConfiguration here, and the omission is deliberate. That value is
        // rendered to the member as "this rule needs changing", and the exception types that would
        // tempt it — ArgumentException, InvalidOperationException — are thrown far more often by the
        // platform's own code than by a rule the member wrote. Blaming a member for a bug in this
        // repository is the one misclassification worse than none. Configuration that is genuinely
        // wrong is caught by the validator on write and by the catalogue lookup on dispatch, both of
        // which set that kind directly and neither of which throws.
        _ => null
    };
}
