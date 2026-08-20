using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Identifiers;

/// <summary>Identifies one option within a poll.</summary>
public sealed record PollOptionId(Guid Value) : StronglyTypedId(Value)
{
    public static PollOptionId New() => new(Guid.NewGuid());

    public static PollOptionId From(Guid value) => new(value);
}
