using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Identifiers;

/// <summary>Identifies a <see cref="Comments.Comment"/>, at any depth in a thread.</summary>
public sealed record CommentId(Guid Value) : StronglyTypedId(Value)
{
    public static CommentId New() => new(Guid.NewGuid());

    public static CommentId From(Guid value) => new(value);
}
