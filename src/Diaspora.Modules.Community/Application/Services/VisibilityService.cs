using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Domain.Visibility;
using Diaspora.Modules.Community.Extensibility.Visibility;
using Diaspora.Modules.Community.Integration;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Application.Services;

/// <summary>
/// Answers whether a reader may see a piece of content.
/// <para>
/// The one place the question is answered, for B6's own content and for every module that
/// consumes <see cref="ICommunityVisibilityService"/>. Everything else — feeds, permalinks,
/// comment threads, bookmarks — routes here, which is what makes it possible to state that a
/// private post is private everywhere rather than in most places.
/// </para>
/// </summary>
public sealed class VisibilityService : ICommunityVisibilityService
{
    private readonly VisibilityEvaluatorRegistry _evaluators;
    private readonly ViewerContextResolver _viewers;
    private readonly IVisibilityRuleRepository _rules;

    public VisibilityService(
        VisibilityEvaluatorRegistry evaluators,
        ViewerContextResolver viewers,
        IVisibilityRuleRepository rules)
    {
        _evaluators = evaluators;
        _viewers = viewers;
        _rules = rules;
    }

    public async Task<bool> CanViewAsync(
        ContentReference content,
        Guid authorId,
        ContentAudience audience,
        UserId? viewerId,
        CancellationToken cancellationToken = default)
    {
        var viewer = await _viewers.ResolveAsync(cancellationToken);

        return _evaluators.IsVisible(await BuildAsync(content, authorId, audience, viewer, cancellationToken));
    }

    public async Task<IReadOnlySet<string>> FilterVisibleAsync(
        IReadOnlyCollection<(ContentReference Content, Guid AuthorId, ContentAudience Audience)> items,
        UserId? viewerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        var viewer = await _viewers.ResolveAsync(cancellationToken);
        var visible = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (content, authorId, audience) in items)
        {
            var request = await BuildAsync(content, authorId, audience, viewer, cancellationToken);

            if (_evaluators.IsVisible(request))
            {
                visible.Add(content.Key);
            }
        }

        return visible;
    }

    public async Task<IReadOnlyCollection<VisibilityLevel>> ResolveAllowedLevelsAsync(
        UserId? viewerId, CancellationToken cancellationToken = default) =>
        ViewerContextResolver.AllowedLevels(await _viewers.ResolveAsync(cancellationToken));

    /// <summary>The context-bearing overload the module's own handlers use.</summary>
    public async Task<bool> CanViewAsync(
        ContentReference content,
        Guid authorId,
        ContentAudience audience,
        ViewerContext viewer,
        string? contentCountryCode = null,
        Guid? contentAreaId = null,
        string? contentHierarchyPath = null,
        CancellationToken cancellationToken = default)
    {
        var request = await BuildAsync(
            content, authorId, audience, viewer, cancellationToken,
            contentCountryCode, contentAreaId, contentHierarchyPath);

        return _evaluators.IsVisible(request);
    }

    private async Task<VisibilityRequest> BuildAsync(
        ContentReference content,
        Guid authorId,
        ContentAudience audience,
        ViewerContext viewer,
        CancellationToken cancellationToken,
        string? contentCountryCode = null,
        Guid? contentAreaId = null,
        string? contentHierarchyPath = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(audience);

        var effective = audience;

        // A custom audience resolves to its stored rule's base level before evaluation. A rule
        // that has been deleted collapses to Private rather than to Public — the safe direction
        // for a missing rule is nobody, not everybody.
        if (audience.Level == VisibilityLevel.Custom && audience.RuleId is { } ruleId)
        {
            var rule = await _rules.GetAsync(ruleId, cancellationToken);

            effective = rule is null
                ? ContentAudience.Private
                : ContentAudience.FromLevel(rule.BaseLevel);
        }

        return new VisibilityRequest
        {
            Content = content,
            AuthorId = authorId,
            Audience = effective,
            ViewerId = viewer.UserId,
            ViewerIsVerified = viewer.IsVerified,
            ViewerVerifiedCountries = viewer.VerifiedCountries,
            ViewerCountryCode = viewer.CountryCode,
            ViewerAreaId = viewer.AreaId,
            ViewerHierarchyPath = viewer.HierarchyPath,
            ViewerFollowsAuthor = viewer.FollowedMemberIds.Contains(authorId),
            IsBlocked = viewer.BlockedMemberIds.Contains(authorId),
            ViewerTier = viewer.Tier,
            ContentCountryCode = contentCountryCode,
            ContentAreaId = contentAreaId,
            ContentHierarchyPath = contentHierarchyPath
        };
    }
}
