using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Identifiers;

/// <summary>
/// Identifies one official notice — an embassy announcement, a visa advisory, a public health
/// warning.
/// <para>
/// Deliberately not a <see cref="GuideId"/>. A guide is written by a member and improves by being
/// edited; a notice is issued by an authority and is superseded rather than corrected. Sharing an
/// identifier type would make the two indistinguishable in a signature at exactly the point where
/// the distinction decides whether a reader is looking at advice or at the law.
/// </para>
/// </summary>
public sealed record OfficialNoticeId(Guid Value) : StronglyTypedId(Value)
{
    public static OfficialNoticeId New() => new(Guid.NewGuid());

    public static OfficialNoticeId From(Guid value) => new(value);
}
