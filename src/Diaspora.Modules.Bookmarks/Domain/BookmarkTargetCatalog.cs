using System.Collections.Frozen;

namespace Diaspora.Modules.Bookmarks.Domain;

/// <summary>
/// The target types a bookmark may point at.
///
/// <para>
/// <b>The question this answers is "what stops a bookmark pointing at something that does not
/// exist".</b> The honest answer has two halves, and only one of them is enforced here.
/// </para>
///
/// <para>
/// <b>The type is validated. The existence of the target is deliberately not.</b>
/// <c>TargetType</c> and <c>TargetId</c> are strings precisely so any module's identifier shape
/// fits without Bookmarks knowing that module exists, and checking existence would mean
/// querying every module Bookmarks can save from — turning a module that deliberately depends
/// on nothing into one that depends on all of them. That trade is the module's whole point, so
/// it is kept, and the consequence is stated rather than hidden: <b>a bookmark can outlive the
/// thing it points at</b>, exactly as a browser bookmark outlives a page. A client resolving a
/// saved item must treat "not found" as ordinary.
/// </para>
///
/// <para>
/// What was wrong was narrower and worth fixing: nothing checked the type at all, so
/// <c>{"targetType":"NotAThing","targetId":"00000000-0000-0000-0000-000000000000"}</c> was
/// accepted and stored as <c>notathing</c>. That is not extensibility, it is an absent
/// invariant — a typo in a client became a permanent row nothing could ever render.
/// </para>
///
/// <para>
/// <b>Extensibility is preserved.</b> A module that owns a new saveable thing adds its key by
/// implementing <see cref="IBookmarkTargetContributor"/>; it does not modify this file and
/// Bookmarks does not reference it. The set below is the platform's own baseline so that the
/// module is safe with no contributors registered at all.
/// </para>
/// </summary>
public sealed class BookmarkTargetCatalog
{
    /// <summary>
    /// The target types the platform ships with. Lower-case and hyphenated, matching the
    /// normalisation the create handler applies before storing.
    /// </summary>
    public static readonly IReadOnlyCollection<string> Baseline = new[]
    {
        "marketplace-listing",
        "housing-listing",
        "job",
        "guide",
        "community-post",
        "event",
        "provider",
        "organization",
        "creator",
        "campaign",
        "knowledge-article",
    };

    private readonly FrozenSet<string> _known;

    public BookmarkTargetCatalog(IEnumerable<IBookmarkTargetContributor>? contributors = null)
    {
        var keys = new HashSet<string>(Baseline, StringComparer.OrdinalIgnoreCase);

        foreach (var key in contributors?.SelectMany(c => c.TargetTypes) ?? [])
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                keys.Add(key.Trim().ToLowerInvariant());
            }
        }

        _known = keys.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    public bool IsKnown(string targetType) =>
        !string.IsNullOrWhiteSpace(targetType)
        && (_known.Contains(targetType.Trim()) || _known.Contains(Normalise(targetType)));

    /// <summary>
    /// <c>CommunityPost</c> → <c>community-post</c>.
    /// <para>
    /// The baseline is written in the hyphenated form, but a caller naming a .NET type or a
    /// client enum member will send PascalCase, and refusing that would be refusing a spelling
    /// of the same word. The keys here were chosen for this catalogue rather than derived from
    /// an existing caller — the module has no client — so accepting both shapes costs nothing
    /// and removes a failure that would be entirely an artefact of which spelling was picked.
    /// </para>
    /// </summary>
    private static string Normalise(string targetType)
    {
        var trimmed = targetType.Trim();
        var builder = new System.Text.StringBuilder(trimmed.Length + 4);

        for (var index = 0; index < trimmed.Length; index++)
        {
            var character = trimmed[index];

            if (char.IsUpper(character) && index > 0 && builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    /// <summary>Every accepted type, ordered, for an error message worth reading.</summary>
    public IReadOnlyList<string> Known => _known.Order(StringComparer.Ordinal).ToArray();
}

/// <summary>
/// Declares the bookmark target types a module owns.
/// <para>
/// The same shape as every other extension point in the kernel — a module says what it has and
/// the platform composes it — so a new saveable thing never requires an edit inside Bookmarks.
/// </para>
/// </summary>
public interface IBookmarkTargetContributor
{
    IEnumerable<string> TargetTypes { get; }
}
