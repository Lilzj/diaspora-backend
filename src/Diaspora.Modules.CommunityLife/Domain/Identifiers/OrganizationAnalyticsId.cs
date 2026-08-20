using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Identifiers;

/// <summary>The measured record kept for one organization.</summary>
public sealed record OrganizationAnalyticsId(Guid Value) : StronglyTypedId(Value)
{
    public static OrganizationAnalyticsId New() => new(Guid.NewGuid());

    public static OrganizationAnalyticsId From(Guid value) => new(value);
}
