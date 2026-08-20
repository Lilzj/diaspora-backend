using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Identifiers;

/// <summary>
/// Identifies one visa or immigration advisory.
/// <para>
/// An advisory describes what the rules are; it never processes an application. That boundary is
/// explicit in the brief and it is why this aggregate has no applicant, no case, no status per
/// person — only requirements, documents, processing times and the dates they are true between.
/// </para>
/// </summary>
public sealed record VisaAdvisoryId(Guid Value) : StronglyTypedId(Value)
{
    public static VisaAdvisoryId New() => new(Guid.NewGuid());

    public static VisaAdvisoryId From(Guid value) => new(value);
}
