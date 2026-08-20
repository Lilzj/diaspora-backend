using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Identifiers;

/// <summary>One member's standing in one organization.</summary>
public sealed record OrganizationMembershipId(Guid Value) : StronglyTypedId(Value)
{
    public static OrganizationMembershipId New() => new(Guid.NewGuid());

    public static OrganizationMembershipId From(Guid value) => new(value);
}
