using System.Globalization;
using Diaspora.Contracts.Automation.V1.Extensibility;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Follows;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Creator.Extensibility.Automation;

/// <summary>
/// B18's answer to what a creator may automate — B23.x Wave 12.
///
/// <para><b>Wave 11 shipped four creator triggers and one creator broadcast action, all working, all
/// granted to nobody.</b> <c>SelfOnlyScopeAuthority</c> withholds <c>automations.creator</c> for a
/// stated reason: "this authority cannot tell a verified creator from anybody else; that is B18's
/// knowledge". This is B18 answering.</para>
///
/// <para><b>Standing is re-read at execution, not trusted from configuration.</b> The automation
/// engine calls <c>EvaluateAsync</c> in the moment before every dispatch, which is the only place a
/// creator suspended since they wrote the rule can be stopped. A restricted or suspended creator
/// keeps their automations and stops being able to run them, which is the difference between a
/// sanction and a deletion.</para>
///
/// <para><b>The bar for owning is lower than the bar for broadcasting, deliberately.</b> Any
/// recognised creator may automate their own affairs — a milestone reaching them, a reward becoming
/// available. Reaching an audience needs a creator the programme has verified, because a broadcast is
/// a message to other people and the platform's whole position on those is that the sender must be
/// somebody it has checked.</para>
/// </summary>
public sealed class CreatorAutomationAuthority : IAutomationScopeAuthority
{
    /// <summary>
    /// The standings that may hold a creator automation at all.
    /// <para>
    /// <c>PendingVerification</c> is included: somebody whose application is with a reviewer is
    /// already contributing, and taking their own notifications away while they wait would punish them
    /// for applying.
    /// </para>
    /// </summary>
    private static readonly CreatorStatus[] MayOwn =
    [
        CreatorStatus.Contributor,
        CreatorStatus.EmergingCreator,
        CreatorStatus.PendingVerification,
        CreatorStatus.VerifiedCreator,
        CreatorStatus.EstablishedCreator,
        CreatorStatus.TopCreator
    ];

    /// <summary>The standings that may reach an audience. Verified and above, and nothing else.</summary>
    private static readonly CreatorStatus[] MayBroadcast =
    [
        CreatorStatus.VerifiedCreator,
        CreatorStatus.EstablishedCreator,
        CreatorStatus.TopCreator
    ];

    private readonly ICreatorProfileRepository _creators;
    private readonly IFollowRepository _follows;

    public CreatorAutomationAuthority(
        ICreatorProfileRepository creators, IFollowRepository follows)
    {
        _creators = creators;
        _follows = follows;
    }

    /// <summary>
    /// Creator scope, and nothing else.
    /// <para>
    /// Naming one kind rather than every kind is what makes the automation platform's resolver prefer
    /// this over the shipped floor: it prefers an authority that is specific, so registration order —
    /// which is assembly scan order — cannot decide a permission.
    /// </para>
    /// </summary>
    public IReadOnlyList<AutomationOwnerKind> Handles { get; } = [AutomationOwnerKind.Creator];

    public async Task<AutomationScopeGrant> EvaluateAsync(
        UserId member,
        AutomationOwnerKind ownerKind,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(member);

        if (ownerKind != AutomationOwnerKind.Creator || member.Value == Guid.Empty)
        {
            return AutomationScopeGrant.None();
        }

        var creator = await _creators.FindByUserAsync(member.Value, cancellationToken);

        if (creator is null)
        {
            return AutomationScopeGrant.None();
        }

        // The scope must be the caller's own creator record. Both forms are accepted — the creator
        // identifier and the member behind it — because an automation may name either, and neither is
        // taken from anywhere but this record.
        if (ownerId != creator.Id.Value && ownerId != creator.UserId)
        {
            return AutomationScopeGrant.None();
        }

        // Suspension and restriction are checked before standing. A top creator under suspension has
        // the highest status on the platform and may do nothing with it.
        if (creator.IsSuspended || creator.IsRestricted)
        {
            return AutomationScopeGrant.None(creator.DisplayName);
        }

        if (!MayOwn.Contains(creator.Status))
        {
            return AutomationScopeGrant.None(creator.DisplayName);
        }

        var mayBroadcast = MayBroadcast.Contains(creator.Status) && creator.IsVerifiedCreator;

        return new AutomationScopeGrant(
            CanOwn: true,
            CanManage: true,
            CanReachMembers: mayBroadcast,
            Label: creator.DisplayName,

            // The capability the four creator triggers and `notify.followers` are gated on. Granted
            // only where the creator may reach members, so a contributor's automations work on their
            // own affairs and cannot broadcast.
            Capabilities: mayBroadcast ? ["automations.creator"] : []);
    }

    public async Task<AutomationAudienceEstimate> EstimateAudienceAsync(
        AutomationOwnerKind ownerKind,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        if (ownerKind != AutomationOwnerKind.Creator)
        {
            return AutomationAudienceEstimate.Unavailable("This is not a creator scope.");
        }

        var creator = await ResolveAsync(ownerId, cancellationToken);

        if (creator is null)
        {
            return AutomationAudienceEstimate.Unavailable("No creator profile answers for this scope.");
        }

        // Counted from the follow graph rather than read off the profile's cached `Followers`. A
        // cached counter is reconciled on a sweep and is right most of the time; an audience estimate
        // shown to somebody about to notify people should be right now.
        var followers = await _follows.CountFollowersAsync(
            FollowTargetType.Member, creator.UserId, cancellationToken);

        // Small audiences are banded rather than reported exactly. "3 people follow you" in a small
        // niche identifies them, which is the same suppression rule B19 applies to a small cohort.
        return followers < 10
            ? new AutomationAudienceEstimate(
                followers, "Fewer than ten people follow you.", IsExact: false)
            : new AutomationAudienceEstimate(
                followers,
                $"About {followers.ToString("N0", CultureInfo.InvariantCulture)} people who follow you.",
                IsExact: true);
    }

    public async Task<IReadOnlyList<UserId>> ListRecipientsAsync(
        AutomationOwnerKind ownerKind,
        Guid ownerId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (ownerKind != AutomationOwnerKind.Creator)
        {
            return [];
        }

        var creator = await ResolveAsync(ownerId, cancellationToken);

        if (creator is null)
        {
            return [];
        }

        // Ascending by when they followed, which is the paging the follow repository grew for this:
        // a descending walk shifts under a follow gained mid-broadcast, so somebody is skipped and
        // somebody is notified twice.
        var page = await _follows.ListFollowerPageAsync(
            FollowTargetType.Member, creator.UserId, skip, take, cancellationToken);

        return [.. page.Select(UserId.From)];
    }

    /// <summary>
    /// The creator a scope identifier names, by either form.
    /// <para>
    /// An automation stores whichever the client sent. Accepting both here rather than normalising on
    /// write means a rule written before Wave 12 keeps working.
    /// </para>
    /// </summary>
    private async Task<Domain.Creators.CreatorProfile?> ResolveAsync(
        Guid ownerId, CancellationToken cancellationToken)
    {
        if (ownerId == Guid.Empty)
        {
            return null;
        }

        var byUser = await _creators.FindByUserAsync(ownerId, cancellationToken);

        return byUser ?? await _creators.GetAsync(
            Domain.Identifiers.CreatorId.From(ownerId), cancellationToken);
    }
}

/// <summary>
/// B18's answer to the <c>creatorStatus</c> condition — B23.x Wave 12.
/// <para>
/// Wave 11 declared it unevaluable with the words "creator status is B18's and this module has no read
/// port for it". This is the read port, and it answers with the programme's own vocabulary — the
/// status name — which is what the frontend's condition editor offers as its choice list.
/// </para>
/// <para>
/// <b>The <em>earned</em> status is not what is answered.</b> A creator whose standing was lowered by
/// a sanction has an <c>EarnedStatus</c> that still says what they achieved and a <c>Status</c> that
/// says what they may do. A condition is about what is true now.
/// </para>
/// </summary>
public sealed class CreatorAutomationConditionSource : IAutomationConditionSource
{
    private readonly ICreatorProfileRepository _creators;

    public CreatorAutomationConditionSource(ICreatorProfileRepository creators) =>
        _creators = creators;

    public IReadOnlyList<string> Answers { get; } = [AutomationConditionKeys.CreatorStatus];

    public async Task<IReadOnlyDictionary<string, string?>> ReadAsync(
        AutomationConditionQuery request, CancellationToken cancellationToken = default)
    {
        var answers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (request.Member.Value == Guid.Empty ||
            !request.ConditionKeys.Contains(
                AutomationConditionKeys.CreatorStatus, StringComparer.Ordinal))
        {
            return answers;
        }

        var creator = await _creators.FindByUserAsync(request.Member.Value, cancellationToken);

        if (creator is null)
        {
            // Not a creator at all. The key is omitted rather than answered `NotACreator`, because a
            // member with no creator record has no creator status to compare and a rule that fired on
            // one would be firing on the platform's default rather than on a fact about them.
            return answers;
        }

        answers[AutomationConditionKeys.CreatorStatus] = creator.Status.ToString();
        return answers;
    }
}
