using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Identifiers;

/// <summary>A posting's analytics rollup identity.</summary>
public sealed record JobAnalyticsId(Guid Value) : StronglyTypedId(Value)
{
    public static JobAnalyticsId New() => new(Guid.NewGuid());

    public static JobAnalyticsId From(Guid value) => new(value);
}
