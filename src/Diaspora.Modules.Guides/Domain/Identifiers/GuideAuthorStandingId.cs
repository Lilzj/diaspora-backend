using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Identifiers;

/// <summary>Identifies the projection row holding what Trust has said about one author.</summary>
public sealed record GuideAuthorStandingId(Guid Value) : StronglyTypedId(Value)
{
    public static GuideAuthorStandingId New() => new(Guid.NewGuid());

    public static GuideAuthorStandingId From(Guid value) => new(value);
}
