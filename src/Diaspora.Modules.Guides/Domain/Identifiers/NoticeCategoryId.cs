using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Identifiers;

/// <summary>
/// Identifies one notice category — <c>visa-advisories</c>, <c>public-holidays</c>,
/// <c>emergency-alerts</c>.
/// <para>
/// The third category identifier in this module, and the three are genuinely different questions.
/// <see cref="GuideCategoryId"/> is where an author files a document they wrote;
/// <see cref="KnowledgeCategoryId"/> is a row a curator puts on a country page; this one is the
/// kind of thing an authority issued. A notice about a passport-office closure is not filed by
/// anybody, is not a navigation row, and its category decides its default severity and how B13
/// treats it.
/// </para>
/// </summary>
public sealed record NoticeCategoryId(Guid Value) : StronglyTypedId(Value)
{
    public static NoticeCategoryId New() => new(Guid.NewGuid());

    public static NoticeCategoryId From(Guid value) => new(value);
}
