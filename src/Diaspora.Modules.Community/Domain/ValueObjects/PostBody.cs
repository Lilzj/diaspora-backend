using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Domain.ValueObjects;

/// <summary>
/// The text of a post.
/// <para>
/// A value object rather than a string because three rules travel with it and would otherwise
/// be re-implemented at every call site: it is trimmed, it is bounded, and it is not blank. The
/// ceiling matches the client composer character counter exactly, which is the only number a
/// member can actually see; a server that allowed more would let a client that stopped counting
/// silently create posts the composer can never edit again.
/// </para>
/// </summary>
public sealed class PostBody : ValueObject
{
    /// <summary>Matches the client composer counter, which renders 0/500.</summary>
    public const int MaxLength = 500;

    /// <summary>
    /// Long-form content — a guide body, an organization announcement — arrives through the same
    /// aggregate with this ceiling instead. Two limits rather than one because a 500-character
    /// cap on a guide would be absurd and a 20,000-character alert would be unreadable.
    /// </summary>
    public const int MaxLongFormLength = 20_000;

    private PostBody(string value) => Value = value;

    private PostBody() { }

    public string Value { get; private set; } = string.Empty;

    public int Length => Value.Length;

    public static Result<PostBody> Create(string? value, bool longForm = false)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return Result.Failure<PostBody>(CommunityErrors.PostBodyEmpty);
        }

        var ceiling = longForm ? MaxLongFormLength : MaxLength;

        return trimmed.Length > ceiling
            ? Result.Failure<PostBody>(CommunityErrors.PostBodyTooLong(ceiling))
            : Result.Success(new PostBody(trimmed));
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
