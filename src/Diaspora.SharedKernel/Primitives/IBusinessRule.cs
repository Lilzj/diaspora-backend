namespace Diaspora.SharedKernel.Primitives;

/// <summary>
/// <b>Superseded by the platform's <c>Result&lt;T&gt;</c> model and retired in PSR-1 Final
/// Certification. Do not implement it.</b>
/// <para>
/// A B1-era domain primitive: a rule object that reports whether it is broken, checked through
/// <c>CheckRule()</c>, which throws <c>BusinessRuleViolationException</c> when it is. <b>No domain
/// rule anywhere on this platform implements it</b> — every module reached for
/// <c>Result&lt;T&gt;</c> and <c>Error</c> instead, and that is the better answer for the reason
/// PSR-1.2's seam audit gave: <b>a broken business rule is a member's mistake, not a bug</b>, and a
/// member's mistake should not travel as an exception.
/// </para>
/// <para>
/// <b>What the alternative actually costs.</b> An exception unwinds the stack past every handler
/// between the rule and the middleware, so the only place that can turn it into a response is a
/// global <c>catch</c> that has lost all the context. <c>Result&lt;T&gt;</c> keeps the failure a
/// value: the handler that raised it is still on the stack, the error carries a code the client
/// switches on, and the compiler makes the caller decide what to do with it. That is why every
/// endpoint on this platform returns <c>Result</c> and none of them catch.
/// </para>
/// <para>
/// <b>It also produced a name collision worth ending.</b> Two types called
/// <c>BusinessRuleViolationException</c> existed — this one and
/// <c>Diaspora.Application.Exceptions.BusinessRuleViolationException</c> — and
/// <c>GlobalExceptionHandlingMiddleware</c> had to disambiguate both by full namespace to map them
/// to the same 422. One of the two is now unusable, which is the outcome that removes the
/// ambiguity without removing the escape hatch.
/// </para>
/// <para>
/// <b>Why obsoleted rather than deleted</b>, following the precedent B1.4.1 set for
/// <c>INotificationPublisher</c>: an empty namespace is an invitation to declare the interface
/// again, and the next author would have no way to know it had been considered and refused.
/// </para>
/// <para>
/// <b>What to use instead.</b> Return <c>Result.Failure(Error.Validation(code, message))</c> from
/// the aggregate method or the handler. See the
/// [Engineering Handbook](../../../docs/platform/engineering-handbook.md).
/// </para>
/// </summary>
[Obsolete(
    "IBusinessRule is superseded by the platform's Result<T> model and must not be implemented. A " +
    "broken business rule is a member's mistake rather than a bug, so it travels as " +
    "Result.Failure(Error.Validation(code, message)) — which keeps the failure a value the caller " +
    "must handle and the client can switch on, instead of an exception that unwinds to a global " +
    "catch. See docs/platform/engineering-handbook.md.",
    error: true,
    DiagnosticId = "DSP4005")]
public interface IBusinessRule
{
    /// <summary>
    /// Determines whether this business rule is broken.
    /// </summary>
    bool IsBroken();

    /// <summary>
    /// The error message when the rule is broken.
    /// </summary>
    string Message { get; }

    /// <summary>
    /// A unique code identifying this rule (e.g., "LST-001").
    /// </summary>
    string Code => GetType().Name;
}
