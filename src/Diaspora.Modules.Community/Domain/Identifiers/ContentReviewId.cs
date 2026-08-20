using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Identifiers;

/// <summary>Identifies one automated review of one piece of content.</summary>
public sealed record ContentReviewId(Guid Value) : StronglyTypedId(Value)
{
    public static ContentReviewId New() => new(Guid.NewGuid());

    public static ContentReviewId From(Guid value) => new(value);
}
