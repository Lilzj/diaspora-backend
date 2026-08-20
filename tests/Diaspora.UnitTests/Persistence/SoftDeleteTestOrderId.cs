using Diaspora.SharedKernel.Primitives;

namespace Diaspora.UnitTests.Persistence;

/// <summary>Identifies a <see cref="SoftDeleteTestOrder"/>.</summary>
public sealed record SoftDeleteTestOrderId(Guid Value) : StronglyTypedId(Value)
{
    public static SoftDeleteTestOrderId New() => new(Guid.NewGuid());
}
