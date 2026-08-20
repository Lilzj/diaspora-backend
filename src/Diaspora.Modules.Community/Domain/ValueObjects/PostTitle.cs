using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Domain.ValueObjects;

/// <summary>
/// A post headline.
/// <para>
/// Optional, and that is a deliberate asymmetry with the client, whose update model requires
/// both a title and a description. The composer only collects a message: the titles in the mock
/// data were written by hand. Requiring one would mean either rejecting everything the composer
/// produces or inventing a title from the first line, and inventing content on a member behalf
/// is not something a feed should do silently.
/// </para>
/// </summary>
public sealed class PostTitle : ValueObject
{
    public const int MaxLength = 200;

    private PostTitle(string value) => Value = value;

    private PostTitle() { }

    public string Value { get; private set; } = string.Empty;

    public static Result<PostTitle?> CreateOptional(string? value)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return Result.Success<PostTitle?>(null);
        }

        return trimmed.Length > MaxLength
            ? Result.Failure<PostTitle?>(CommunityErrors.PostTitleTooLong(MaxLength))
            : Result.Success<PostTitle?>(new PostTitle(trimmed));
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
