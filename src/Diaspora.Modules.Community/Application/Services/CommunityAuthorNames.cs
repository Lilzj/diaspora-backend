using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Application.Services;

/// <summary>
/// The display names of the members who wrote what a response carries.
/// <para>
/// <b>The gap this closes.</b> Community stores no member profile, so <c>AuthorMapper</c> left the
/// author's name null and every post, comment and feed card — including the reader's own — was
/// rendered as "Community member". The name belongs to Identity and reaches modules through
/// <see cref="IMemberProfileService"/>; this is the one place Community asks it.
/// </para>
/// <para>
/// <b>Once per response.</b> A caller collects every author identifier on the page and resolves
/// them in a single call; the mappers then look names up in the dictionary. A service that cannot
/// answer yields an empty dictionary, and the names stay null as before — an unanswered question
/// is not a placeholder.
/// </para>
/// </summary>
public sealed class CommunityAuthorNames
{
    private static readonly IReadOnlyDictionary<Guid, string> None = new Dictionary<Guid, string>();

    private readonly IMemberProfileService _members;

    public CommunityAuthorNames(IMemberProfileService members)
    {
        _members = members;
    }

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
}
