namespace Diaspora.SharedKernel.Results;

/// <summary>
/// <b>Retired in PSR-1 Final Certification. Do not use it.</b>
/// <para>
/// Never had a consumer. See <see cref="OperationResult"/> for the finding this closes (B2.5
/// DDD-02).
/// </para>
/// <para>
/// <b>The one thing it offered that <c>Result</c> does not — a list of errors — it offers
/// misleadingly.</b> It derives from <c>Result</c> and passes only <c>errors[0]</c> to the base, so
/// every consumer that treats it as the <c>Result</c> it claims to be sees exactly one error and
/// silently loses the rest. A caller that genuinely needs to report several validation failures at
/// once should carry them in the error's own payload, where nothing can drop them.
/// </para>
/// </summary>
[Obsolete(
    "The parallel result types in Diaspora.SharedKernel.Results are retired. Use Result and " +
    "Error. This type also loses every error but the first to any consumer holding it as a " +
    "Result — B2.5 DDD-02. See docs/platform/engineering-handbook.md.",
    error: true,
    DiagnosticId = "DSP4005")]
public sealed class ValidationResult : Result
{
    public IReadOnlyList<Error> Errors { get; }

    private ValidationResult(IReadOnlyList<Error> errors)
        : base(false, errors.Count > 0 ? errors[0] : Error.Validation("Validation.General", "Validation failed."))
    {
        Errors = errors;
    }

    public static ValidationResult WithErrors(IReadOnlyList<Error> errors) => new(errors);

    public static ValidationResult WithErrors(params Error[] errors) => new(errors);
}
