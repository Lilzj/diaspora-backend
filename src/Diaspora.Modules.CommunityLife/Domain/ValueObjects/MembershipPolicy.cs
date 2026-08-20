using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Domain.ValueObjects;

/// <summary>
/// How an organization admits members.
/// <para>
/// <b>Configurable per organization because the right answer genuinely differs.</b> A church admits
/// anyone who walks in; an alumni association checks you went there; a professional body has a
/// committee. A platform that picked one policy would be wrong for the other two, and one that
/// offered only "open or closed" would push the middle case into a comment box.
/// </para>
/// <para>
/// <see cref="Requirements"/> is free text and stays so. What a body asks of its members is written
/// in its constitution, not chosen from a list, and an enum here would be a list of somebody
/// else's rules.
/// </para>
/// </summary>
public sealed class MembershipPolicy : ValueObject
{
    private MembershipPolicy(
        MembershipPolicyKind kind,
        string? requirements,
        bool requiresApproval,
        int? renewalMonths,
        bool isMembershipPublic)
    {
        Kind = kind;
        Requirements = requirements;
        RequiresApproval = requiresApproval;
        RenewalMonths = renewalMonths;
        IsMembershipPublic = isMembershipPublic;
    }

    private MembershipPolicy() { }

    /// <summary>Anyone may join, nothing is asked, and the member list is public.</summary>
    public static MembershipPolicy Open =>
        new(MembershipPolicyKind.Open, null, false, null, true);

    public MembershipPolicyKind Kind { get; private set; }

    /// <summary>What the organization asks of its members, in its own words.</summary>
    public string? Requirements { get; private set; }

    /// <summary>Whether an administrator has to say yes. Implied by the kind, and stored.</summary>
    public bool RequiresApproval { get; private set; }

    /// <summary>
    /// How long a membership lasts before it lapses, in months. Null means it does not lapse,
    /// which is the case for nearly every community organization and not for a subscription body.
    /// </summary>
    public int? RenewalMonths { get; private set; }

    /// <summary>
    /// Whether anybody may see who belongs.
    /// <para>
    /// <b>Defaults to visible and is genuinely a safety setting when it is not.</b> Membership of a
    /// religious or political association is sensitive information in some of the countries this
    /// platform's members come from, and an organization that turns this off must have the member
    /// list hidden from everyone outside it — which the query handlers enforce, not just the client.
    /// </para>
    /// </summary>
    public bool IsMembershipPublic { get; private set; }

    public bool AcceptsSelfService => Kind is MembershipPolicyKind.Open
        or MembershipPolicyKind.RequestToJoin;

    public static Result<MembershipPolicy> Create(
        MembershipPolicyKind kind,
        string? requirements = null,
        int? renewalMonths = null,
        bool isMembershipPublic = true)
    {
        if (renewalMonths is { } months && (months < 1 || months > 120))
        {
            return Result.Failure<MembershipPolicy>(
                CommunityLifeErrors.MembershipPolicyInvalid);
        }

        var trimmed = requirements?.Trim();

        if (trimmed is { Length: > CommunityLifeLimits.MaxMembershipRequirements })
        {
            trimmed = trimmed[..CommunityLifeLimits.MaxMembershipRequirements];
        }

        return Result.Success(new MembershipPolicy(
            kind,
            string.IsNullOrWhiteSpace(trimmed) ? null : trimmed,
            kind is MembershipPolicyKind.RequestToJoin or MembershipPolicyKind.InviteOnly,
            renewalMonths,
            isMembershipPublic));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Kind;
        yield return Requirements;
        yield return RequiresApproval;
        yield return RenewalMonths;
        yield return IsMembershipPublic;
    }
}
