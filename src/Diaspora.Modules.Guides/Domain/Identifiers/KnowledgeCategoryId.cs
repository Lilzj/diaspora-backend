using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Identifiers;

/// <summary>
/// Identifies one navigation node of the Country Knowledge Directory.
/// <para>
/// Deliberately not <see cref="GuideCategoryId"/>. A guide category is where a guide is
/// <em>filed</em>; a knowledge category is what a country page <em>shows</em>, in what order,
/// under what localized label, drawing from one or more guide categories. Sharing an identifier
/// would make the two impossible to tell apart in a signature, which is exactly the confusion
/// B8.1 exists to remove.
/// </para>
/// </summary>
public sealed record KnowledgeCategoryId(Guid Value) : StronglyTypedId(Value)
{
    public static KnowledgeCategoryId New() => new(Guid.NewGuid());

    public static KnowledgeCategoryId From(Guid value) => new(value);
}
