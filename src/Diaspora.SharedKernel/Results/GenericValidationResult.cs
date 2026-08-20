namespace Diaspora.SharedKernel.Results;

/// <summary>
/// <b>Retired in PSR-1 Final Certification. Do not use it.</b> The generic sibling of
/// <see cref="ValidationResult"/>, with the same never-used history and the same
/// every-error-but-the-first defect. Use <c>Result&lt;T&gt;</c> and <c>Error</c>.
/// </summary>
[Obsolete(
    "The parallel result types in Diaspora.SharedKernel.Results are retired. Use Result<T> and " +
    "Error — B2.5 DDD-02. See docs/platform/engineering-handbook.md.",
    error: true,
    DiagnosticId = "DSP4005")]
public sealed class GenericValidationResult<T> : Result<T>
{
    public IReadOnlyList<Error> Errors { get; }

    private GenericValidationResult(IReadOnlyList<Error> errors)
        : base(errors.Count > 0 ? errors[0] : Error.Validation("Validation.General", "Validation failed."))
    {
        Errors = errors;
    }

    private GenericValidationResult(Error error)
        : base(error)
    {
        Errors = [error];
    }

    public static GenericValidationResult<T> WithErrors(IReadOnlyList<Error> errors) => new(errors);

    public static GenericValidationResult<T> WithErrors(params Error[] errors) => new(errors.ToList());
}
