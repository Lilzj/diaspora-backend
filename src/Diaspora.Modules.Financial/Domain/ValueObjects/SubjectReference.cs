using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.ValueObjects;

/// <summary>
/// What a purchase, a placement or a contribution is about, named without knowing what it is.
/// <para>
/// <b>A type and an identifier, and deliberately nothing else.</b> This module never resolves a
/// subject, never joins to one, never validates that it exists and never renders one — doing any of
/// those would need a reference to the module that owns it, which is the single thing the module
/// graph forbids and the reason the Financial Platform can be the sole owner of money without
/// becoming a dependency of every vertical.
/// </para>
/// <para>
/// The consequence is worth stating plainly: <b>buying a boost for a listing that does not exist
/// will succeed.</b> That is correct. The alternative is a synchronous call into Marketplace on the
/// payment path, which would make a marketplace outage a payment outage. The subject is validated
/// by the vertical when it consumes the published placement contract, and a placement nobody claims
/// expires on its own.
/// </para>
/// </summary>
public sealed class SubjectReference : ValueObject
{
    private SubjectReference(MonetizationSubjectType subjectType, Guid subjectId)
    {
        SubjectType = subjectType;
        SubjectId = subjectId;
    }

    private SubjectReference() { }

    public MonetizationSubjectType SubjectType { get; private set; }

    /// <summary>The identifier in the owning module. Opaque here.</summary>
    public Guid SubjectId { get; private set; }

    public static SubjectReference Create(MonetizationSubjectType subjectType, Guid subjectId) =>
        new(subjectType, subjectId);

    /// <summary>The subject of a purchase that is about the buyer rather than about a thing.</summary>
    public static SubjectReference ForMember(Guid userId) =>
        new(MonetizationSubjectType.Member, userId);

    public bool Matches(MonetizationSubjectType subjectType, Guid subjectId) =>
        SubjectType == subjectType && SubjectId == subjectId;

    public override string ToString() => $"{SubjectType}:{SubjectId}";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return SubjectType;
        yield return SubjectId;
    }
}
