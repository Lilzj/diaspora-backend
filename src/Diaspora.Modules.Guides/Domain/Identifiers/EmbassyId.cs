using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Identifiers;

/// <summary>
/// Identifies one diplomatic mission in the embassy directory.
/// <para>
/// <b>Not a <c>ProviderId</c>, and the separation is a decision rather than an oversight.</b> A
/// provider is a member's business: it registers itself, it is owned by an account, it is reviewed
/// by customers and it can be suspended. An embassy is none of those things — it is managed content
/// about an institution that will never hold an account here, and modelling it as a provider would
/// invite reviews, ownership claims and a suspension workflow for a consulate.
/// </para>
/// </summary>
public sealed record EmbassyId(Guid Value) : StronglyTypedId(Value)
{
    public static EmbassyId New() => new(Guid.NewGuid());

    public static EmbassyId From(Guid value) => new(value);
}
