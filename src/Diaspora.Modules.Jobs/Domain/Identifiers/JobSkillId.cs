using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Identifiers;

/// <summary>A catalogued skill's identity.</summary>
public sealed record JobSkillId(Guid Value) : StronglyTypedId(Value)
{
    public static JobSkillId New() => new(Guid.NewGuid());

    public static JobSkillId From(Guid value) => new(value);
}
