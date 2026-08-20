using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>A community fund contributions accumulate into.</summary>
public sealed record ContributionFundId(Guid Value) : StronglyTypedId(Value)
{
    public static ContributionFundId New() => new(Guid.NewGuid());

    public static ContributionFundId From(Guid value) => new(value);
}
