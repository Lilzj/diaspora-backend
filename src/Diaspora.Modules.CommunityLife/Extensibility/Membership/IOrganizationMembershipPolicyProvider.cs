namespace Diaspora.Modules.CommunityLife.Extensibility.Membership;

/// <summary>
/// How a deployment adds rules about who may join an organization.
/// <para>
/// <b>The brief says membership policies should be configurable, and the four kinds on
/// <c>MembershipPolicy</c> are only the shipped ones.</b> An alumni association that wants to check
/// a graduation year, a professional body that wants to check a registration number, a student
/// group that wants to check a university email domain — each is a class implementing this
/// interface, and none of them is a change to this module.
/// </para>
/// <para>
/// <b>Providers run in order and the first refusal wins.</b> A policy that says no is not
/// overridden by one that says yes, because the alternative would mean a deployment could not add a
/// restriction without removing something else.
/// </para>
/// </summary>
public interface IOrganizationMembershipPolicyProvider
{
    string Name { get; }

    /// <summary>Whether this provider has an opinion about this organization at all.</summary>
    bool AppliesTo(MembershipDecisionContext context) => true;

    /// <summary>
    /// What should happen to the request.
    /// <para>
    /// Return null to abstain. Abstaining is different from admitting: it means the next provider
    /// decides, and if none does, the organization's own configured policy applies.
    /// </para>
    /// </summary>
    Task<MembershipDecision?> DecideAsync(
        MembershipDecisionContext context, CancellationToken cancellationToken = default);
}
