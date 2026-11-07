using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Contracts.Moderation.V1;
using Diaspora.Domain.Identity.Administration;
using Diaspora.Domain.Identity.Users;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Application.Identity.Mapping;

/// <summary>Projects a proposed account action onto the wire, with the people it names.</summary>
public static class AccountActionProposalMapper
{
    public static AccountActionProposalResponse ToResponse(
        AccountActionProposal proposal, IReadOnlyDictionary<UserId, User> people)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(people);

        var target = people.GetValueOrDefault(proposal.TargetUserId);

        return new AccountActionProposalResponse
        {
            Id = proposal.Id.Value,
            Kind = proposal.Kind.ToString(),
            TargetUserId = proposal.TargetUserId.Value,
            TargetFullName = target?.FullName.Value,
            TargetRole = target?.Role.Name,
            ProposedBy = proposal.ProposedBy?.Value,
            ProposedByName = proposal.ProposedBy is { } proposer
                ? people.GetValueOrDefault(proposer)?.FullName.Value
                : null,
            RaisedByCaseId = proposal.RaisedByCaseId,
            AwaitsProposer = proposal.AwaitsProposer,
            ReasonCode = proposal.ReasonCode,
            ReasonLabel = ModerationReasons.TryParse(proposal.ReasonCode, out var code)
                ? ModerationReasons.Label(code)
                : null,
            Note = proposal.Note,
            Until = proposal.Until,
            Role = proposal.Role,
            Status = proposal.Status.ToString(),
            DecidedBy = proposal.DecidedBy?.Value,
            DecidedByName = proposal.DecidedBy is { } decider
                ? people.GetValueOrDefault(decider)?.FullName.Value
                : null,
            DecidedAt = proposal.DecidedAt,
            DecisionNote = proposal.DecisionNote,
            CreatedAt = proposal.CreatedAt
        };
    }
}
