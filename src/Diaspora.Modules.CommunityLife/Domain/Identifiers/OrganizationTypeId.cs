using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Identifiers;

/// <summary>A row in the configurable organization-type catalogue.</summary>
public sealed record OrganizationTypeId(Guid Value) : StronglyTypedId(Value)
{
    public static OrganizationTypeId New() => new(Guid.NewGuid());

    public static OrganizationTypeId From(Guid value) => new(value);
}
