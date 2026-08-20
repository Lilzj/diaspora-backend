namespace Diaspora.SharedKernel.Results;

/// <summary>
/// <b>Retired in PSR-1 Final Certification. Do not use it.</b>
/// <para>
/// One of four parallel result types in this namespace, none of which has ever had a consumer.
/// <b>B2.5's production-readiness audit found all four and named the risk exactly</b> — DDD-02,
/// <em>"Six result types where one plus <c>Error</c> suffices … New module authors will copy
/// whichever they find first."</em> The finding was recorded, never closed, and never reached the
/// Technical Debt Ledger, which is why it survived fifteen phases.
/// </para>
/// <para>
/// <b>What the platform uses instead:</b> <c>Result</c>, <c>Result&lt;T&gt;</c> and <c>Error</c>.
/// Every handler, every endpoint and every aggregate method on this platform returns one, and the
/// error carries a code the client switches on. This type carries an <see cref="Exception"/> and a
/// loose string instead — no code, nothing a client can branch on, and an exception smuggled through
/// a value.
/// </para>
/// <para>
/// Kept rather than deleted, per B1.4.1's precedent: an empty namespace is an invitation to declare
/// the type again, and the next author would have no way to know it had been considered and refused.
/// </para>
/// </summary>
[Obsolete(
    "The parallel result types in Diaspora.SharedKernel.Results are retired. The platform " +
    "expresses success and failure with Result, Result<T> and Error, which every handler, " +
    "endpoint and aggregate method already returns. Six result types where one plus Error " +
    "suffices is how a new module author picks whichever they find first — B2.5 DDD-02. See " +
    "docs/platform/engineering-handbook.md.",
    error: true,
    DiagnosticId = "DSP4005")]
public sealed class OperationResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public Exception? Exception { get; }

    private OperationResult(bool isSuccess, string? errorMessage = null, Exception? exception = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Exception = exception;
    }

    public static OperationResult Success() => new(true);

    public static OperationResult Failure(string errorMessage, Exception? exception = null) =>
        new(false, errorMessage, exception);

    public static OperationResult FromException(Exception exception) =>
        new(false, exception.Message, exception);
}
