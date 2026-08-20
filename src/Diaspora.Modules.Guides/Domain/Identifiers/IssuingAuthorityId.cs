using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Identifiers;

/// <summary>
/// Identifies one issuing authority — a ministry, an immigration department, a police service.
/// <para>
/// An authority is the answer to "who says so", and it is the field that makes a notice official
/// rather than merely posted. It is a managed record rather than a member account: nothing on this
/// platform authenticates as a government, and B8.2 does not pretend otherwise.
/// </para>
/// </summary>
public sealed record IssuingAuthorityId(Guid Value) : StronglyTypedId(Value)
{
    public static IssuingAuthorityId New() => new(Guid.NewGuid());

    public static IssuingAuthorityId From(Guid value) => new(value);
}
