using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Identifiers;

/// <summary>Identifies the analytics row that accumulates one guide's usage counters.</summary>
public sealed record GuideAnalyticsId(Guid Value) : StronglyTypedId(Value)
{
    public static GuideAnalyticsId New() => new(Guid.NewGuid());

    public static GuideAnalyticsId From(Guid value) => new(value);
}
