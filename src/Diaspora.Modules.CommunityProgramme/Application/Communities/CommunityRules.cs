using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Requests;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Application.Communities;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// A community's rules — FRONTEND-PHASE13-GAP-016, 21 Oct 2026.
//
// Read by anybody who may read the community: rules are what somebody deciding whether to join
// wants to see first, so they are as public as the community is. Written only by those
// `CommunityCapabilitySet.CanManageRules` names — the owner and administrators — with no staff
// bypass: a platform moderator has no business deciding what a community asks of its members.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>A community's rules, in order.</summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record ListCommunityRulesQuery : IQuery<IReadOnlyList<CommunityRuleResponse>>
{
    public required string IdOrSlug { get; init; }
}

public sealed class ListCommunityRulesQueryHandler
    : IQueryHandler<ListCommunityRulesQuery, IReadOnlyList<CommunityRuleResponse>>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly ICommunityRuleRepository _rules;
    private readonly CommunityProgrammeAccessService _access;

    public ListCommunityRulesQueryHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityRuleRepository rules,
        CommunityProgrammeAccessService access)
    {
        _communities = communities;
        _memberships = memberships;
        _rules = rules;
        _access = access;
    }

    public async Task<Result<IReadOnlyList<CommunityRuleResponse>>> Handle(
        ListCommunityRulesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var readable = await CommunityReadAccess.ResolveAsync(
            query.IdOrSlug, _communities, _memberships, _access, cancellationToken);

        if (readable is null)
        {
            return Result.Failure<IReadOnlyList<CommunityRuleResponse>>(CommunityErrors.NotFound);
        }

        var rules = await _rules.ListAsync(readable.Community.Id, cancellationToken);

        return Result.Success<IReadOnlyList<CommunityRuleResponse>>(
            [.. rules.Select(CommunityRuleMapper.ToResponse)]);
    }
}

/// <summary>Add a rule. The owner's or an administrator's.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Participate)]
public sealed record CreateCommunityRuleCommand : ICommand<CommunityRuleResponse>
{
    public required string IdOrSlug { get; init; }

    public required SaveCommunityRuleRequest Request { get; init; }
}

public sealed class CreateCommunityRuleCommandHandler
    : ICommandHandler<CreateCommunityRuleCommand, CommunityRuleResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly ICommunityRuleRepository _rules;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCommunityRuleCommandHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityRuleRepository rules,
        CommunityProgrammeAccessService access,
        IUnitOfWork unitOfWork)
    {
        _communities = communities;
        _memberships = memberships;
        _rules = rules;
        _access = access;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CommunityRuleResponse>> Handle(
        CreateCommunityRuleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var managed = await CommunityRuleMapper.RequireManagerAsync(
            command.IdOrSlug, _communities, _memberships, _access, cancellationToken);

        if (managed.IsFailure)
        {
            return Result.Failure<CommunityRuleResponse>(managed.Error);
        }

        var community = managed.Value.Community;
        var existing = (await _rules.ListAsync(community.Id, cancellationToken)).ToList();

        if (existing.Count >= CommunityRule.MaximumPerCommunity)
        {
            return Result.Failure<CommunityRuleResponse>(CommunityGovernanceErrors.TooManyRules);
        }

        var created = CommunityRule.Create(
            CommunityRuleId.New(),
            community.Id,
            existing.Count + 1,
            command.Request.Title,
            command.Request.Body,
            command.Request.Consequence);

        if (created.IsFailure)
        {
            return Result.Failure<CommunityRuleResponse>(created.Error);
        }

        var rule = created.Value;
        _rules.Add(rule);

        existing.Add(rule);
        CommunityRuleMapper.Place(existing, rule, command.Request.Position);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(CommunityRuleMapper.ToResponse(rule));
    }
}

/// <summary>Replace a rule's text, and optionally move it. The owner's or an administrator's.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Participate)]
public sealed record UpdateCommunityRuleCommand : ICommand<CommunityRuleResponse>
{
    public required string IdOrSlug { get; init; }

    public required Guid RuleId { get; init; }

    public required SaveCommunityRuleRequest Request { get; init; }
}

public sealed class UpdateCommunityRuleCommandHandler
    : ICommandHandler<UpdateCommunityRuleCommand, CommunityRuleResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly ICommunityRuleRepository _rules;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCommunityRuleCommandHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityRuleRepository rules,
        CommunityProgrammeAccessService access,
        IUnitOfWork unitOfWork)
    {
        _communities = communities;
        _memberships = memberships;
        _rules = rules;
        _access = access;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CommunityRuleResponse>> Handle(
        UpdateCommunityRuleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var managed = await CommunityRuleMapper.RequireManagerAsync(
            command.IdOrSlug, _communities, _memberships, _access, cancellationToken);

        if (managed.IsFailure)
        {
            return Result.Failure<CommunityRuleResponse>(managed.Error);
        }

        var rules = (await _rules.ListAsync(managed.Value.Community.Id, cancellationToken)).ToList();
        var rule = rules.FirstOrDefault(candidate => candidate.Id.Value == command.RuleId);

        if (rule is null)
        {
            return Result.Failure<CommunityRuleResponse>(CommunityGovernanceErrors.RuleNotFound);
        }

        var revised = rule.Revise(
            command.Request.Title, command.Request.Body, command.Request.Consequence);

        if (revised.IsFailure)
        {
            return Result.Failure<CommunityRuleResponse>(revised.Error);
        }

        CommunityRuleMapper.Place(rules, rule, command.Request.Position);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(CommunityRuleMapper.ToResponse(rule));
    }
}

/// <summary>Remove a rule. Answers the rules that remain, renumbered.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Participate)]
public sealed record DeleteCommunityRuleCommand : ICommand<IReadOnlyList<CommunityRuleResponse>>
{
    public required string IdOrSlug { get; init; }

    public required Guid RuleId { get; init; }
}

public sealed class DeleteCommunityRuleCommandHandler
    : ICommandHandler<DeleteCommunityRuleCommand, IReadOnlyList<CommunityRuleResponse>>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly ICommunityRuleRepository _rules;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCommunityRuleCommandHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityRuleRepository rules,
        CommunityProgrammeAccessService access,
        IUnitOfWork unitOfWork)
    {
        _communities = communities;
        _memberships = memberships;
        _rules = rules;
        _access = access;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyList<CommunityRuleResponse>>> Handle(
        DeleteCommunityRuleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var managed = await CommunityRuleMapper.RequireManagerAsync(
            command.IdOrSlug, _communities, _memberships, _access, cancellationToken);

        if (managed.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CommunityRuleResponse>>(managed.Error);
        }

        var rules = (await _rules.ListAsync(managed.Value.Community.Id, cancellationToken)).ToList();
        var rule = rules.FirstOrDefault(candidate => candidate.Id.Value == command.RuleId);

        if (rule is null)
        {
            return Result.Failure<IReadOnlyList<CommunityRuleResponse>>(
                CommunityGovernanceErrors.RuleNotFound);
        }

        _rules.Remove(rule);
        rules.Remove(rule);
        CommunityRuleMapper.Renumber(rules);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CommunityRuleResponse>>(
            [.. rules.Select(CommunityRuleMapper.ToResponse)]);
    }
}

internal static class CommunityRuleMapper
{
    public static CommunityRuleResponse ToResponse(CommunityRule rule) => new()
    {
        Id = rule.Id.Value,
        Position = rule.Position,
        Title = rule.Title,
        Body = rule.Body,
        Consequence = rule.Consequence,
        UpdatedAt = rule.ModifiedAt ?? (rule.CreatedAt == default ? null : rule.CreatedAt)
    };

    /// <summary>
    /// The community, if the caller may change its rules. Not found where they may not even read it
    /// — a refusal would confirm a private community exists — and forbidden where they may read it
    /// but do not run it.
    /// </summary>
    public static async Task<Result<ReadableCommunity>> RequireManagerAsync(
        string idOrSlug,
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        CommunityProgrammeAccessService access,
        CancellationToken cancellationToken)
    {
        var caller = access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<ReadableCommunity>(caller.Error);
        }

        var readable = await CommunityReadAccess.ResolveAsync(
            idOrSlug, communities, memberships, access, cancellationToken);

        if (readable is null)
        {
            return Result.Failure<ReadableCommunity>(CommunityErrors.NotFound);
        }

        // The community's own decision, so no staff bypass — the capability set is derived without
        // one, exactly as ChangeCommunityRoleCommandHandler derives it.
        var capabilities = CommunityCapabilitySet.For(readable.Community, readable.Membership);

        return capabilities.CanManageRules
            ? Result.Success(readable)
            : Result.Failure<ReadableCommunity>(CommunityGovernanceErrors.CannotManageRules);
    }

    /// <summary>
    /// Moves <paramref name="rule"/> to <paramref name="position"/> (clamped to the list) and
    /// renumbers everything one to n. With no position the rule keeps its place in the order.
    /// </summary>
    public static void Place(List<CommunityRule> ordered, CommunityRule rule, int? position)
    {
        if (position is { } wanted)
        {
            ordered.Remove(rule);
            ordered.Insert(Math.Clamp(wanted - 1, 0, ordered.Count), rule);
        }

        Renumber(ordered);
    }

    public static void Renumber(IReadOnlyList<CommunityRule> ordered)
    {
        for (var index = 0; index < ordered.Count; index++)
        {
            if (ordered[index].Position != index + 1)
            {
                ordered[index].MoveTo(index + 1);
            }
        }
    }
}
