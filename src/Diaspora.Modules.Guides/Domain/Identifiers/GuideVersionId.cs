using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Identifiers;

/// <summary>Identifies one immutable published snapshot of a guide.</summary>
public sealed record GuideVersionId(Guid Value) : StronglyTypedId(Value)
{
    public static GuideVersionId New() => new(Guid.NewGuid());

    public static GuideVersionId From(Guid value) => new(value);
}
