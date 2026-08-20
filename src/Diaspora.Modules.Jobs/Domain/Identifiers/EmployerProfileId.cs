using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Identifiers;

/// <summary>An employer profile's identity. The organisation behind a posting.</summary>
public sealed record EmployerProfileId(Guid Value) : StronglyTypedId(Value)
{
    public static EmployerProfileId New() => new(Guid.NewGuid());

    public static EmployerProfileId From(Guid value) => new(value);
}
