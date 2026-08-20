using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Identifiers;

/// <summary>Identifies one member's vote on one poll option.</summary>
public sealed record PollVoteId(Guid Value) : StronglyTypedId(Value)
{
    public static PollVoteId New() => new(Guid.NewGuid());

    public static PollVoteId From(Guid value) => new(value);
}
