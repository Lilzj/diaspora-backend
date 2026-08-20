namespace Diaspora.SharedKernel.Results;

/// <summary>
/// Represents the outcome of an operation that returns a value on success.
/// </summary>
public class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(true, Error.None)
    {
        _value = value;
    }

    protected Result(Error error) : base(false, error)
    {
        _value = default;
    }

    /// <summary>
    /// The value of a successful result. Throws if accessed on a failed result.
    /// </summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failed result.");

    public static Result<T> Success(T value) => new(value);
    public new static Result<T> Failure(Error error) => new(error);

    /// <summary>
    /// Implicit conversion from value to successful result.
    /// </summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>
    /// Implicit conversion from Error to failed result.
    /// </summary>
    public static implicit operator Result<T>(Error error) => Failure(error);

    /// <summary>
    /// Maps the value of a successful result to a new type.
    /// </summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> mapper) =>
        IsSuccess ? Result<TOut>.Success(mapper(_value!)) : Result<TOut>.Failure(Error);

    /// <summary>
    /// Binds the value of a successful result to a new result.
    /// </summary>
    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> binder) =>
        IsSuccess ? binder(_value!) : Result<TOut>.Failure(Error);

    /// <summary>
    /// Matches on success or failure, returning a value.
    /// </summary>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(Error);
}
