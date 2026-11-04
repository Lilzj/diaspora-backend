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

    /// <summary>
    /// The authors' names, keyed by user identifier. The dictionary is a
    /// <see cref="CommunityAuthorDictionary"/>, which also holds each author's photo; the mappers
    /// read it through <c>AuthorMapper.AvatarOf</c>.
    /// </summary>
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

        return new CommunityAuthorDictionary(profiles);
    }
}

/// <summary>
/// The authors' names, and their photos alongside.
/// <para>
/// <b>Why a dictionary that knows more.</b> Twenty call sites resolve a page's authors once and
/// hand the name lookup down to <c>AuthorMapper</c>. A member who set a profile photo still showed
/// as initials beside every post and comment (UAT, 3 Nov 2026), and threading a second lookup
/// through all twenty would be twenty chances to forget it. So the lookup they already pass is this
/// one, which answers names exactly as before and carries the photos with it. A lookup built some
/// other way simply has no photos, and the author's photo stays null.
/// </para>
/// </summary>
public sealed class CommunityAuthorDictionary : IReadOnlyDictionary<Guid, string>
{
    private readonly Dictionary<Guid, string> _names;
    private readonly Dictionary<Guid, string> _avatars;

    public CommunityAuthorDictionary(IReadOnlyDictionary<Guid, MemberProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        _names = profiles.ToDictionary(pair => pair.Key, pair => pair.Value.DisplayName);
        _avatars = profiles
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value.AvatarUrl))
            .ToDictionary(pair => pair.Key, pair => pair.Value.AvatarUrl!);
    }

    /// <summary>The member's photo address, or null when they have none.</summary>
    public string? AvatarOf(Guid userId) => _avatars.TryGetValue(userId, out var url) ? url : null;

    public string this[Guid key] => _names[key];

    public IEnumerable<Guid> Keys => _names.Keys;

    public IEnumerable<string> Values => _names.Values;

    public int Count => _names.Count;

    public bool ContainsKey(Guid key) => _names.ContainsKey(key);

    public bool TryGetValue(Guid key, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out string value) =>
        _names.TryGetValue(key, out value);

    public IEnumerator<KeyValuePair<Guid, string>> GetEnumerator() => _names.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
