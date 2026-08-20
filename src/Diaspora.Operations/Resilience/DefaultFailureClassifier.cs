using System.Net.Sockets;
using Diaspora.Platform.Operations.Resilience;

namespace Diaspora.Operations.Resilience;

/// <summary>
/// The platform's default view of which failures are worth retrying.
/// <para>
/// <b>Conservative by construction: anything it does not recognise is permanent.</b> The alternative
/// default — retry unless proven otherwise — retries validation errors, authorisation failures and
/// business-rule violations, none of which will ever succeed, and does so against a dependency that
/// is working fine. On a non-idempotent operation it does worse than waste time.
/// </para>
/// <para>
/// A dependency whose client library signals transience differently registers its own
/// <see cref="IFailureClassifier"/> naming that dependency, and the executor prefers it.
/// </para>
/// </summary>
public sealed class DefaultFailureClassifier : IFailureClassifier
{
    /// <summary>Null means "the platform default", used when no dependency-specific classifier exists.</summary>
    public string? Dependency => null;

    public FailureClassification Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            OperationCanceledException => FailureClassification.Cancelled,

            // A timeout is the archetypal transient failure and the one most worth retrying:
            // the dependency may well have been fine and merely slow.
            TimeoutException => FailureClassification.Transient,

            // Connection refused, reset, DNS failure, host unreachable. All are "the call did not
            // reach a working dependency", which is exactly what a retry is for.
            SocketException => FailureClassification.Transient,
            HttpRequestException http => ClassifyHttp(http),
            IOException => FailureClassification.Transient,

            // A circuit that is open means the dependency was not called. Retrying inside the same
            // executor would spin against a breaker that is deliberately refusing; the caller above
            // decides what to do instead.
            CircuitOpenException => FailureClassification.Permanent,
            BulkheadFullException => FailureClassification.Permanent,

            // Argument, invalid-operation, not-supported, and every domain exception the platform
            // defines. Retrying produces the same answer more slowly.
            _ => FailureClassification.Permanent
        };
    }

    /// <summary>
    /// Reads the status code when there is one.
    /// <para>
    /// 408, 429 and 5xx are the retryable set. 429 in particular: the dependency has said "later",
    /// which is a request to retry rather than a refusal — though the backoff, not the retry count,
    /// is what makes honouring it useful.
    /// </para>
    /// </summary>
    private static FailureClassification ClassifyHttp(HttpRequestException exception) =>
        exception.StatusCode switch
        {
            null => FailureClassification.Transient,
            System.Net.HttpStatusCode.RequestTimeout => FailureClassification.Transient,
            System.Net.HttpStatusCode.TooManyRequests => FailureClassification.Transient,
            >= System.Net.HttpStatusCode.InternalServerError => FailureClassification.Transient,
            _ => FailureClassification.Permanent
        };
}
