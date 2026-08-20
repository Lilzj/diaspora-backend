using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Identifiers;

/// <summary>
/// Identifies one country's knowledge directory.
/// <para>
/// A surrogate rather than the country code itself, even though the code is unique and is what
/// every URL carries. The platform's persistence conventions — the audit interceptor, the soft
/// delete converter, the outbox correlation — all key on a <c>Guid</c> primary key, and a
/// two-character natural key would be the one aggregate that opts out of all three. The code
/// carries a unique index instead, which is where the uniqueness actually needs enforcing.
/// </para>
/// </summary>
public sealed record CountryKnowledgeDirectoryId(Guid Value) : StronglyTypedId(Value)
{
    public static CountryKnowledgeDirectoryId New() => new(Guid.NewGuid());

    public static CountryKnowledgeDirectoryId From(Guid value) => new(value);
}
