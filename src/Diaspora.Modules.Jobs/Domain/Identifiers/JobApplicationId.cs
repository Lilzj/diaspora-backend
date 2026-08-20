using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Identifiers;

/// <summary>An application's identity. The durable record of one candidate approaching one job.</summary>
public sealed record JobApplicationId(Guid Value) : StronglyTypedId(Value)
{
    public static JobApplicationId New() => new(Guid.NewGuid());

    public static JobApplicationId From(Guid value) => new(value);
}
