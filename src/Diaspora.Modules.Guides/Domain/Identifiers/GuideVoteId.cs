using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Identifiers;

/// <summary>Identifies one member's helpfulness vote on one guide.</summary>
public sealed record GuideVoteId(Guid Value) : StronglyTypedId(Value)
{
    public static GuideVoteId New() => new(Guid.NewGuid());

    public static GuideVoteId From(Guid value) => new(value);
}
