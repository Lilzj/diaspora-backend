using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Identifiers;

/// <summary>A listing analytics rollup's identity.</summary>
public sealed record ListingAnalyticsId(Guid Value) : StronglyTypedId(Value)
{
    public static ListingAnalyticsId New() => new(Guid.NewGuid());

    public static ListingAnalyticsId From(Guid value) => new(value);
}
