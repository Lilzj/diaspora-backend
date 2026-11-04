using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Guides.Application.Services;

/// <summary>
/// The display names of the members who wrote guides.
/// <para>
/// <b>The gap this closes.</b> Guides stores the author's identifier and Trust's standing, but no
/// name, so every card and byline was anonymous. The name belongs to Identity and reaches modules
/// through <see cref="IMemberProfileService"/>; this is the one place Guides asks it.
/// </para>
/// <para>
/// <b>Once per page.</b> <see cref="ToCardsAsync"/> resolves every author on the page in a single
/// call. A service that cannot answer leaves the names null — an unanswered question is not a
/// placeholder.
/// </para>
/// </summary>
public sealed class GuideAuthorNames
{
    private static readonly IReadOnlyDictionary<Guid, string> None = new Dictionary<Guid, string>();

    private readonly IMemberProfileService _members;

    public GuideAuthorNames(IMemberProfileService members)
    {
        _members = members;
    }

    /// <summary>The members' display names, keyed by user identifier.</summary>
    public async Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(
        IEnumerable<UserId> authorIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorIds);

        var distinct = authorIds.Distinct().ToList();

        if (distinct.Count == 0 || !_members.IsAvailable)
        {
            return None;
        }

        var profiles = await _members.ResolveAsync(distinct, cancellationToken);

        return profiles.ToDictionary(pair => pair.Key, pair => pair.Value.DisplayName);
    }

    /// <summary>
    /// One author's profile — name and photo — or null when it cannot be resolved. The byline
    /// shows the photo beside the name (UAT, 3 Nov 2026: a member who added one still saw their
    /// initials).
    /// </summary>
    public async Task<MemberProfile?> ProfileOfAsync(UserId authorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorId);

        if (!_members.IsAvailable)
        {
            return null;
        }

        var profiles = await _members.ResolveAsync([authorId], cancellationToken);

        return profiles.GetValueOrDefault(authorId.Value);
    }

    /// <summary>One author's display name, or null when it cannot be resolved.</summary>
    public async Task<string?> NameOfAsync(UserId authorId, CancellationToken cancellationToken)
    {
        var names = await ResolveAsync([authorId], cancellationToken);

        return names.GetValueOrDefault(authorId.Value);
    }

    /// <summary>Cards for these guides, each carrying its author's name.</summary>
    public async Task<IReadOnlyList<GuideCardResponse>> ToCardsAsync(
        IEnumerable<Guide> guides, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(guides);

        var list = guides as IReadOnlyList<Guide> ?? guides.ToList();
        var names = await ResolveAsync(list.Select(guide => guide.AuthorId), cancellationToken);

        return list.Select(guide => GuideCardMapper.ToCard(guide, names)).ToList();
    }
}
