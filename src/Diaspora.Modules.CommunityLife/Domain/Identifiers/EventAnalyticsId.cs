using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Identifiers;

/// <summary>The measured record kept for one event.</summary>
public sealed record EventAnalyticsId(Guid Value) : StronglyTypedId(Value)
{
    public static EventAnalyticsId New() => new(Guid.NewGuid());

    public static EventAnalyticsId From(Guid value) => new(value);
}
