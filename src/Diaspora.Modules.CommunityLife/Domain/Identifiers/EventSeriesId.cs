using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Identifiers;

/// <summary>
/// A recurring event series' identity.
/// <para>
/// Distinct from <see cref="CommunityEventId"/> and never interchangeable with it. A series is the
/// rule that generates occasions; an occurrence is one of the occasions. Both are addressable, and
/// a client that confused the two would register somebody for a rule.
/// </para>
/// </summary>
public sealed record EventSeriesId(Guid Value) : StronglyTypedId(Value)
{
    public static EventSeriesId New() => new(Guid.NewGuid());

    public static EventSeriesId From(Guid value) => new(value);
}
