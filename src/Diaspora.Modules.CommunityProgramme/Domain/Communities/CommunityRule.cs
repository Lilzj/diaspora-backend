using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Domain.Communities;

/// <summary>
/// A rule or guideline a community publishes to its members — FRONTEND-PHASE13-GAP-016, 21 Oct 2026.
/// <para>
/// <b>Its own row rather than a list on the community.</b> Rules are edited one at a time by the
/// people who run the community, each has a stable identifier the client keys on, and the community
/// aggregate is loaded on every directory read — carrying fifty paragraphs of text on it would make
/// every browse pay for a page almost nobody opens.
/// </para>
/// <para>
/// <b>Who may write one is not decided here.</b> <see cref="CommunityCapabilitySet.CanManageRules"/>
/// is the answer, read by the handler; this type only guarantees that what is stored is a rule.
/// </para>
/// </summary>
public sealed class CommunityRule : AggregateRoot<CommunityRuleId>, IAuditableEntity
{
    public const int TitleMaximumLength = 120;
    public const int BodyMaximumLength = 2000;
    public const int ConsequenceMaximumLength = 500;

    /// <summary>More than this is a policy document, and a policy document is not what this screen is.</summary>
    public const int MaximumPerCommunity = 50;

    private CommunityRule(CommunityRuleId id, CommunityId communityId) : base(id)
    {
        CommunityId = communityId;
    }

    // EF Core.
    private CommunityRule() { }

    public CommunityId CommunityId { get; private set; } = null!;

    /// <summary>One-based, and contiguous within a community. The handler keeps it so.</summary>
    public int Position { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    /// <summary>What happens to somebody who breaks it, where the community says. Rendered verbatim.</summary>
    public string? Consequence { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static Result<CommunityRule> Create(
        CommunityRuleId id,
        CommunityId communityId,
        int position,
        string? title,
        string? body,
        string? consequence)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(communityId);

        var rule = new CommunityRule(id, communityId) { Position = Math.Max(1, position) };
        var revised = rule.Revise(title, body, consequence);

        return revised.IsFailure
            ? Result.Failure<CommunityRule>(revised.Error)
            : Result.Success(rule);
    }

    /// <summary>Replaces the text. All three fields at once, as the editor sends them.</summary>
    public Result Revise(string? title, string? body, string? consequence)
    {
        var trimmedTitle = title?.Trim();
        var trimmedBody = body?.Trim();
        var trimmedConsequence = string.IsNullOrWhiteSpace(consequence) ? null : consequence.Trim();

        if (string.IsNullOrEmpty(trimmedTitle))
        {
            return Result.Failure(CommunityGovernanceErrors.RuleTitleRequired);
        }

        if (trimmedTitle.Length > TitleMaximumLength)
        {
            return Result.Failure(CommunityGovernanceErrors.RuleTitleLength);
        }

        if (string.IsNullOrEmpty(trimmedBody))
        {
            return Result.Failure(CommunityGovernanceErrors.RuleBodyRequired);
        }

        if (trimmedBody.Length > BodyMaximumLength)
        {
            return Result.Failure(CommunityGovernanceErrors.RuleBodyLength);
        }

        if (trimmedConsequence is { Length: > ConsequenceMaximumLength })
        {
            return Result.Failure(CommunityGovernanceErrors.RuleConsequenceLength);
        }

        Title = trimmedTitle;
        Body = trimmedBody;
        Consequence = trimmedConsequence;

        return Result.Success();
    }

    /// <summary>Moves the rule. The handler renumbers its siblings so positions stay contiguous.</summary>
    public void MoveTo(int position) => Position = Math.Max(1, position);

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}
