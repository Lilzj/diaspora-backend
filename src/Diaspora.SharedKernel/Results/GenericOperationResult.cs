namespace Diaspora.SharedKernel.Results;

/// <summary>
/// <b>Retired in PSR-1 Final Certification. Do not use it.</b> See <see cref="OperationResult"/>
/// for the reasoning; this is its generic sibling and has never had a consumer either. Use
/// <c>Result&lt;T&gt;</c> and <c>Error</c>.
/// </summary>
[Obsolete(
    "The parallel result types in Diaspora.SharedKernel.Results are retired. Use Result<T> and " +
    "Error — B2.5 DDD-02. See docs/platform/engineering-handbook.md.",
    error: true,
    DiagnosticId = "DSP4005")]
public sealed class GenericOperationResult<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? ErrorMessage { get; }
    public Exception? Exception { get; }

    private GenericOperationResult(bool isSuccess, T? value = default, string? errorMessage = null, Exception? exception = null)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorMessage = errorMessage;
        Exception = exception;
    }

    public static GenericOperationResult<T> Success(T value) => new(true, value);

    public static GenericOperationResult<T> Failure(string errorMessage, Exception? exception = null) =>
        new(false, errorMessage: errorMessage, exception: exception);

    public static GenericOperationResult<T> FromException(Exception exception) =>
        new(false, errorMessage: exception.Message, exception: exception);
}
