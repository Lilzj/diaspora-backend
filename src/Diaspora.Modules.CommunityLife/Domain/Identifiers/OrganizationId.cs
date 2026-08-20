using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Identifiers;

/// <summary>A community organization's identity.</summary>
public sealed record OrganizationId(Guid Value) : StronglyTypedId(Value)
{
    public static OrganizationId New() => new(Guid.NewGuid());

    public static OrganizationId From(Guid value) => new(value);
}
