using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Identifiers;

/// <summary>Identifies a community improvement suggestion on a guide.</summary>
public sealed record GuideSuggestionId(Guid Value) : StronglyTypedId(Value)
{
    public static GuideSuggestionId New() => new(Guid.NewGuid());

    public static GuideSuggestionId From(Guid value) => new(value);
}
