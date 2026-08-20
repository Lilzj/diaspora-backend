using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Identifiers;

/// <summary>An offer's identity. The terms an employer put in front of a candidate.</summary>
public sealed record JobOfferId(Guid Value) : StronglyTypedId(Value)
{
    public static JobOfferId New() => new(Guid.NewGuid());

    public static JobOfferId From(Guid value) => new(value);
}
