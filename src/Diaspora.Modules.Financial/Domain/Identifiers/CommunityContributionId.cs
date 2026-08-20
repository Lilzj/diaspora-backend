using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>One contribution to a community fund.</summary>
public sealed record CommunityContributionId(Guid Value) : StronglyTypedId(Value)
{
    public static CommunityContributionId New() => new(Guid.NewGuid());

    public static CommunityContributionId From(Guid value) => new(value);
}
