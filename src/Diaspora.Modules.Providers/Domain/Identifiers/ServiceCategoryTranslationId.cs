using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Identifiers;

/// <summary>Identifies a category's name and description in one language.</summary>
public sealed record ServiceCategoryTranslationId(Guid Value) : StronglyTypedId(Value)
{
    public static ServiceCategoryTranslationId New() => new(Guid.NewGuid());

    public static ServiceCategoryTranslationId From(Guid value) => new(value);
}
