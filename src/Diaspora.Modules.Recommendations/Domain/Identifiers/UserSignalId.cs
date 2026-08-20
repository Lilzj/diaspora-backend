using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.Identifiers;

/// <summary>One observed fact about one member. Append-only, and never updated after it is written.</summary>
public sealed record UserSignalId(Guid Value) : StronglyTypedId(Value)
{
    public static UserSignalId New() => new(Guid.NewGuid());

    public static UserSignalId From(Guid value) => new(value);
}
