using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Identifiers;

/// <summary>A standing job alert's identity.</summary>
public sealed record JobAlertId(Guid Value) : StronglyTypedId(Value)
{
    public static JobAlertId New() => new(Guid.NewGuid());

    public static JobAlertId From(Guid value) => new(value);
}
