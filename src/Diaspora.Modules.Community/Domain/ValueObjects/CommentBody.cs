using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Domain.ValueObjects;

/// <summary>
/// The text of a comment.
/// <para>
/// Separate from <see cref="PostBody"/> despite looking identical, because the two ceilings are
/// independent product decisions that will diverge. Sharing one type would make raising the
/// comment limit a change to posts as well, and the first person to do it would not notice.
/// </para>
/// </summary>
public sealed class CommentBody : ValueObject
{
    public const int MaxLength = 2_000;

    private CommentBody(string value) => Value = value;

    private CommentBody() { }

    public string Value { get; private set; } = string.Empty;

    public static Result<CommentBody> Create(string? value)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return Result.Failure<CommentBody>(CommunityErrors.CommentBodyEmpty);
        }

        return trimmed.Length > MaxLength
            ? Result.Failure<CommentBody>(CommunityErrors.CommentBodyTooLong(MaxLength))
            : Result.Success(new CommentBody(trimmed));
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
