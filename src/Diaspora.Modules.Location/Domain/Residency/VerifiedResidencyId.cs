using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Residency;

/// <summary>Identifies a <see cref="VerifiedResidency"/> projection row.</summary>
public sealed record VerifiedResidencyId(Guid Value) : StronglyTypedId(Value)
{
    public static VerifiedResidencyId New() => new(Guid.NewGuid());

    public static VerifiedResidencyId From(Guid value) => new(value);
}
