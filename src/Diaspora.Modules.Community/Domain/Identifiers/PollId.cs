using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Identifiers;

/// <summary>Identifies a poll. A poll belongs to exactly one post.</summary>
public sealed record PollId(Guid Value) : StronglyTypedId(Value)
{
    public static PollId New() => new(Guid.NewGuid());

    public static PollId From(Guid value) => new(value);
}
