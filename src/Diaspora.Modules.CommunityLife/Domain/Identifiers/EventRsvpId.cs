using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Identifiers;

/// <summary>One member's intention to attend one event, and everything that came of it.</summary>
public sealed record EventRsvpId(Guid Value) : StronglyTypedId(Value)
{
    public static EventRsvpId New() => new(Guid.NewGuid());

    public static EventRsvpId From(Guid value) => new(value);
}
