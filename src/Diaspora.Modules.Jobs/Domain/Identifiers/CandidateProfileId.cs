using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Identifiers;

/// <summary>
/// A candidate profile's identity.
/// <para>
/// Distinct from the member's <c>UserId</c> even though a member holds at most one. The
/// profile is a professional presentation with its own lifetime — created long after the
/// account, deletable without closing it — and giving it its own identity keeps an
/// application's foreign key pointing at the thing it actually references.
/// </para>
/// </summary>
public sealed record CandidateProfileId(Guid Value) : StronglyTypedId(Value)
{
    public static CandidateProfileId New() => new(Guid.NewGuid());

    public static CandidateProfileId From(Guid value) => new(value);
}
