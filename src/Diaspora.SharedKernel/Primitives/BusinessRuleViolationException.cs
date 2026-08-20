namespace Diaspora.SharedKernel.Primitives;

/// <summary>
/// <b>Retired with <see cref="IBusinessRule"/> in PSR-1 Final Certification. Do not throw it.</b>
/// <para>
/// One of two types that carried this name. This one wraps a broken <see cref="IBusinessRule"/>;
/// the other, <c>Diaspora.Application.Exceptions.BusinessRuleViolationException</c>, takes a message
/// and a rule code and remains the supported escape hatch for the rare case where a rule failure
/// genuinely cannot be returned as a value. Both mapped to the same 422, and
/// <c>GlobalExceptionHandlingMiddleware</c> had to name both by full namespace to tell them apart —
/// which is the symptom this retirement removes.
/// </para>
/// <para>
/// See <see cref="IBusinessRule"/> for why the platform returns
/// <c>Result.Failure(Error.Validation(code, message))</c> instead, and why the type is obsoleted
/// rather than deleted.
/// </para>
/// </summary>
[Obsolete(
    "Diaspora.SharedKernel.Primitives.BusinessRuleViolationException is retired with IBusinessRule " +
    "and must not be thrown. Return Result.Failure(Error.Validation(code, message)). Where an " +
    "exception is genuinely unavoidable, Diaspora.Application.Exceptions." +
    "BusinessRuleViolationException is the supported type and maps to the same 422. See " +
    "docs/platform/engineering-handbook.md.",
    error: true,
    DiagnosticId = "DSP4005")]
public sealed class BusinessRuleViolationException : Exception
{
    public IBusinessRule BrokenRule { get; }
    public string RuleCode => BrokenRule.Code;

    public BusinessRuleViolationException(IBusinessRule brokenRule)
        : base(brokenRule.Message)
    {
        BrokenRule = brokenRule;
    }
}
