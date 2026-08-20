using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Identifiers;

/// <summary>An employment-domain category's identity.</summary>
public sealed record JobCategoryId(Guid Value) : StronglyTypedId(Value)
{
    public static JobCategoryId New() => new(Guid.NewGuid());

    public static JobCategoryId From(Guid value) => new(value);
}
