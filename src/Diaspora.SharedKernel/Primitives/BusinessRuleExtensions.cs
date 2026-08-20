namespace Diaspora.SharedKernel.Primitives;

/// <summary>
/// <b>Retired with <see cref="IBusinessRule"/> in PSR-1 Final Certification. Do not call it.</b>
/// <para>
/// The throwing half of the retired rule primitive. See <see cref="IBusinessRule"/> for why the
/// platform expresses a broken rule as <c>Result.Failure(Error.Validation(code, message))</c>
/// rather than an exception, and why the type is obsoleted rather than deleted.
/// </para>
/// </summary>
[Obsolete(
    "CheckRule is retired with IBusinessRule and must not be called. Return " +
    "Result.Failure(Error.Validation(code, message)) instead of throwing. See " +
    "docs/platform/engineering-handbook.md.",
    error: true,
    DiagnosticId = "DSP4005")]
public static class BusinessRuleExtensions
{
    /// <summary>
    /// Checks a business rule and throws BusinessRuleViolationException if broken.
    /// </summary>
    public static void CheckRule(this IBusinessRule rule)
    {
        if (rule.IsBroken())
        {
            throw new BusinessRuleViolationException(rule);
        }
    }
}
