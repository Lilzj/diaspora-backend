using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Presence;

/// <summary>Identifies a <see cref="MemberPresence"/> row.</summary>
public sealed record MemberPresenceId(Guid Value) : StronglyTypedId(Value)
{
    public static MemberPresenceId New() => new(Guid.NewGuid());

    public static MemberPresenceId From(Guid value) => new(value);
}
