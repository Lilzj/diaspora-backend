using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Identifiers;

/// <summary>One community-contributed observation about a neighbourhood.</summary>
public sealed record NeighborhoodInsightId(Guid Value) : StronglyTypedId(Value)
{
    public static NeighborhoodInsightId New() => new(Guid.NewGuid());

    public static NeighborhoodInsightId From(Guid value) => new(value);
}
