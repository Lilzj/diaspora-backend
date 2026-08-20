using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Memberships;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Extensibility.Membership;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.CommunityLife.Application.Services;

/// <summary>
/// Decides what happens to a request to join.
/// <para>
/// <b>Contributed providers run first and the first refusal wins.</b> A policy that says no is not
/// overridden by one that says yes, because the alternative would mean a deployment could not add a
/// restriction without removing something else. A provider that abstains leaves the decision to the
/// next one, and if none decides, the organization's own configured policy applies.
/// </para>
/// <para>
/// <b>Somebody an administrator removed cannot rejoin themselves.</b> That is checked here rather
/// than in the aggregate, because it is a question about the organization's policy and the member's
/// history together — and it is the asymmetry that makes <c>Left</c> and <c>Removed</c> worth being
/// separate states.
/// </para>
/// </summary>
public sealed class CommunityLifeMembershipPolicyEvaluator
{
    private readonly IReadOnlyList<IOrganizationMembershipPolicyProvider> _providers;
    private readonly ILogger<CommunityLifeMembershipPolicyEvaluator> _logger;

    public CommunityLifeMembershipPolicyEvaluator(
        IEnumerable<IOrganizationMembershipPolicyProvider> providers,
        ILogger<CommunityLifeMembershipPolicyEvaluator> logger)
    {
        _providers = [.. providers];
        _logger = logger;
    }

    /// <summary>What should happen to an application.</summary>
    public async Task<MembershipDecision> DecideAsync(
        Organization organization,
        UserId applicantUserId,
        OrganizationMembership? existing,
        string? requestMessage,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organization);
        ArgumentNullException.ThrowIfNull(applicantUserId);

        // A removal is a decision by the organization about this person, and only the organization
        // can undo it. Checked before any provider runs, so no contributed policy can wave it.
        if (existing?.Status == MembershipStatus.Removed)
        {
            return MembershipDecision.Refuse("An administrator removed you from this organization.");
        }

        var context = new MembershipDecisionContext
        {
            OrganizationId = organization.Id.Value,
            OrganizationTypeKey = organization.OrganizationTypeKey,
            ApplicantUserId = applicantUserId.Value,
            PolicyKind = organization.MembershipPolicy.Kind,
            Requirements = organization.MembershipPolicy.Requirements,
            RequestMessage = requestMessage,
            PreviousStatus = existing?.Status,
            CurrentMemberCount = organization.Statistics.MemberCount,
            RequestedAt = now
        };

        foreach (var provider in _providers)
        {
            if (!provider.AppliesTo(context))
            {
                continue;
            }

            try
            {
                var decision = await provider.DecideAsync(context, cancellationToken);

                if (decision is null)
                {
                    continue;
                }

                // First refusal wins; anything else is taken as the answer and stops the chain.
                return decision;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // A contributed policy must not stop somebody joining a church.
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Membership policy {Provider} failed for organization {OrganizationId}. Skipping it.",
                    provider.Name,
                    organization.Id.Value);
            }
#pragma warning restore CA1031
        }

        return organization.MembershipPolicy.Kind switch
        {
            MembershipPolicyKind.Open => MembershipDecision.Admit(),
            MembershipPolicyKind.RequestToJoin => MembershipDecision.Queue(),
            MembershipPolicyKind.InviteOnly => MembershipDecision.Refuse(
                "This organization admits members by invitation."),
            _ => MembershipDecision.Refuse("This organization is not taking members at the moment.")
        };
    }
}
