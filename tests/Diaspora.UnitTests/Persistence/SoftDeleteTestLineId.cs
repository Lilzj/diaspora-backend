using Diaspora.SharedKernel.Primitives;

namespace Diaspora.UnitTests.Persistence;

/// <summary>Identifies a <see cref="SoftDeleteTestLine"/>.</summary>
public sealed record SoftDeleteTestLineId(Guid Value) : StronglyTypedId(Value)
{
    public static SoftDeleteTestLineId New() => new(Guid.NewGuid());
}
