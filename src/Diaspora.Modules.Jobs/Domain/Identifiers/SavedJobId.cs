using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Identifiers;

/// <summary>A saved job's identity.</summary>
public sealed record SavedJobId(Guid Value) : StronglyTypedId(Value)
{
    public static SavedJobId New() => new(Guid.NewGuid());

    public static SavedJobId From(Guid value) => new(value);
}
