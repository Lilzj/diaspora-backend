using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityProgramme.Domain.Identifiers;

/// <summary>
/// A membership programme — one per organization.
/// <para>
/// Its own identifier rather than the organization's, even though the relation is one-to-one. An
/// organization that never opens a programme has no row here at all, and reusing the organization's
/// identifier would make "has a programme" and "is an organization" the same question.
/// </para>
/// </summary>
public sealed record MembershipProgrammeId(Guid Value) : StronglyTypedId(Value)
{
    public static MembershipProgrammeId New() => new(Guid.NewGuid());

    public static MembershipProgrammeId From(Guid value) => new(value);
}

/// <summary>
/// One priced tier inside a programme.
/// <para>
/// The client puts this in a URL and an application names it, so it crosses the boundary. It is
/// still an owned row on the programme: a tier without its programme is not a thing.
/// </para>
/// </summary>
public sealed record MembershipTierId(Guid Value) : StronglyTypedId(Value)
{
    public static MembershipTierId New() => new(Guid.NewGuid());

    public static MembershipTierId From(Guid value) => new(value);
}

/// <summary>
/// One member's standing in one organization's programme.
/// <para>
/// <b>Distinct from B12's <c>OrganizationMembershipId</c>, and the two must not be conflated.</b>
/// B12's edge answers "does this person belong here, and what may they do". This one answers "which
/// tier did they buy, and where is that in its fourteen-state life". A member can hold either
/// without the other: somebody can be an organization's Owner and have never bought a membership,
/// and somebody can hold a lapsed paid membership and no role at all.
/// </para>
/// </summary>
public sealed record ProgrammeMembershipId(Guid Value) : StronglyTypedId(Value)
{
    public static ProgrammeMembershipId New() => new(Guid.NewGuid());

    public static ProgrammeMembershipId From(Guid value) => new(value);
}

/// <summary>A piece of volunteer work an organization is asking for.</summary>
public sealed record VolunteerOpportunityId(Guid Value) : StronglyTypedId(Value)
{
    public static VolunteerOpportunityId New() => new(Guid.NewGuid());

    public static VolunteerOpportunityId From(Guid value) => new(value);
}

/// <summary>One dated slot on an opportunity.</summary>
public sealed record VolunteerShiftId(Guid Value) : StronglyTypedId(Value)
{
    public static VolunteerShiftId New() => new(Guid.NewGuid());

    public static VolunteerShiftId From(Guid value) => new(value);
}

/// <summary>Somebody offering to do it.</summary>
public sealed record VolunteerApplicationId(Guid Value) : StronglyTypedId(Value)
{
    public static VolunteerApplicationId New() => new(Guid.NewGuid());

    public static VolunteerApplicationId From(Guid value) => new(value);
}

/// <summary>An approved volunteer placed on a shift.</summary>
public sealed record VolunteerAssignmentId(Guid Value) : StronglyTypedId(Value)
{
    public static VolunteerAssignmentId New() => new(Guid.NewGuid());

    public static VolunteerAssignmentId From(Guid value) => new(value);
}

/// <summary>An issued record of confirmed hours.</summary>
public sealed record VolunteerCertificateId(Guid Value) : StronglyTypedId(Value)
{
    public static VolunteerCertificateId New() => new(Guid.NewGuid());

    public static VolunteerCertificateId From(Guid value) => new(value);
}

/// <summary>A question put to a community.</summary>
public sealed record CommunityPollId(Guid Value) : StronglyTypedId(Value)
{
    public static CommunityPollId New() => new(Guid.NewGuid());

    public static CommunityPollId From(Guid value) => new(value);
}

/// <summary>One answer on a poll. Owned by the poll and identified for the client's benefit.</summary>
public sealed record PollOptionId(Guid Value) : StronglyTypedId(Value)
{
    public static PollOptionId New() => new(Guid.NewGuid());

    public static PollOptionId From(Guid value) => new(value);
}

/// <summary>
/// One member's vote.
/// <para>
/// Its own aggregate rather than a collection on the poll, and the reason is the same one B12 gives
/// for memberships: a poll put to a country has as many responses as there are members, and loading
/// them to render the question would be absurd. It is also what makes one-member-one-vote a unique
/// index rather than a check inside a loaded list.
/// </para>
/// </summary>
public sealed record PollResponseId(Guid Value) : StronglyTypedId(Value)
{
    public static PollResponseId New() => new(Guid.NewGuid());

    public static PollResponseId From(Guid value) => new(value);
}

/// <summary>A scoped, published post from an organization to the people it can address.</summary>
public sealed record CommunityAnnouncementId(Guid Value) : StronglyTypedId(Value)
{
    public static CommunityAnnouncementId New() => new(Guid.NewGuid());

    public static CommunityAnnouncementId From(Guid value) => new(value);
}

/// <summary>
/// A community — the group people belong to, not the organization that serves one.
/// <para>
/// The register asked for "a stable identifier independent of country code", and this is it. A
/// community keyed by country could never express "Nigerians in London" and "Nigerians in Toronto"
/// as two things, and could never express an interest community at all.
/// </para>
/// </summary>
public sealed record CommunityId(Guid Value) : StronglyTypedId(Value)
{
    public static CommunityId New() => new(Guid.NewGuid());
    public static CommunityId From(Guid value) => new(value);
}

/// <summary>
/// One person's standing in one community.
/// <para>
/// Distinct from <see cref="ProgrammeMembershipId"/>, which is a paid tier in an organization's
/// programme. Somebody can belong to a community and have bought nothing, and can hold a lapsed
/// paid membership of an organization that serves a community they never joined.
/// </para>
/// </summary>
public sealed record CommunityMembershipId(Guid Value) : StronglyTypedId(Value)
{
    public static CommunityMembershipId New() => new(Guid.NewGuid());
    public static CommunityMembershipId From(Guid value) => new(value);
}
