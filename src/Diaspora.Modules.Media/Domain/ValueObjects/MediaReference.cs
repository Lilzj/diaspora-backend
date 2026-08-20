using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Media.Domain.ValueObjects;

/// <summary>
/// The thing a business module stores instead of a URL.
/// <para>
/// This is the single most important type in B5, and the constraint it enforces is worth
/// stating plainly: <b>a business module holds an identifier and a role, and nothing else.</b>
/// Not a URL, not a storage key, not a bucket name, not a CDN host. Every one of those leaks a
/// storage decision into a table that will outlive the storage decision.
/// </para>
/// <para>
/// The cost of getting this wrong is not hypothetical. The frontend today stores raw Unsplash
/// URLs in thirty-one fields across seventeen types; migrating a provider means rewriting
/// every one of those rows, and no rewrite can fix the copies clients already cached. A
/// reference costs one join and makes provider migration invisible to every consumer.
/// </para>
/// <para>
/// <see cref="Role"/> is what disambiguates several references to the same owner: a housing
/// listing has one <c>cover</c> and n <c>gallery</c> images, and without a role the two are
/// indistinguishable at the point of use.
/// </para>
/// </summary>
public sealed class MediaReference : ValueObject
{
    public const int MaxRoleLength = 64;

    private MediaReference(MediaId mediaId, MediaOwner owner, string role, int position)
    {
        MediaId = mediaId;
        Owner = owner;
        Role = role;
        Position = position;
    }

    /// <summary>The asset. The only thing a consumer needs to ask Media for a link.</summary>
    public MediaId MediaId { get; }

    public MediaOwner Owner { get; }

    /// <summary>What the asset is to its owner: <c>cover</c>, <c>gallery</c>, <c>avatar</c>.</summary>
    public string Role { get; }

    /// <summary>Order within a role. Zero when the role holds a single asset.</summary>
    public int Position { get; }

    public static MediaReference Create(
        MediaId mediaId, MediaOwner owner, string role, int position = 0)
    {
        ArgumentNullException.ThrowIfNull(mediaId);
        ArgumentNullException.ThrowIfNull(owner);

        var normalized = Normalize(role);

        return new MediaReference(mediaId, owner, normalized, position < 0 ? 0 : position);
    }

    public MediaReference AtPosition(int position) =>
        new(MediaId, Owner, Role, position < 0 ? 0 : position);

    public override string ToString() => $"{Owner}/{Role}[{Position}]→{MediaId.Value:N}";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MediaId;
        yield return Owner;
        yield return Role;
        yield return Position;
    }

    /// <summary>
    /// Lower-cased and reduced to a slug. A role is compared and indexed, and
    /// <c>Cover</c> versus <c>cover</c> versus <c>coverImage</c> is exactly the fragmentation
    /// the frontend already suffers from.
    /// </summary>
    private static string Normalize(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return MediaReferenceRoles.Attachment;
        }

        var normalized = new string(role
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsAsciiLetterOrDigit(character) ? character : '-')
            .ToArray())
            .Trim('-');

        if (normalized.Length == 0)
        {
            return MediaReferenceRoles.Attachment;
        }

        return normalized.Length <= MaxRoleLength ? normalized : normalized[..MaxRoleLength];
    }
}
