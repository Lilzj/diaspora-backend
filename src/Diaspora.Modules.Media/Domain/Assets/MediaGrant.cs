using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Media.Domain.Assets;

/// <summary>
/// One member's explicit permission over one asset — the module's <c>MediaPermission</c>.
/// <para>
/// Exists because visibility is too coarse for the case that matters most. A verification
/// document is private and must stay private; a reviewer needs to read exactly that document,
/// for exactly as long as the review takes, and the fact that they read it must be
/// reconstructable afterwards. Widening the document's visibility would give it to every
/// reviewer forever.
/// </para>
/// <para>
/// An entity inside <see cref="MediaAsset"/>. Grants are never queried across assets and never
/// outlive the asset they are about, and holding them inside the boundary is what makes
/// "a revoked grant cannot be used" a single check rather than a join every caller must
/// remember.
/// </para>
/// <para>
/// Revocation is a flag, not a delete. "Who could see this document, and when did that stop"
/// is a question a data-protection request will eventually ask, and a deleted row cannot
/// answer it.
/// </para>
/// </summary>
public sealed class MediaGrant : Entity<MediaGrantId>
{
    public MediaId MediaId { get; private set; } = default!;

    public UserId GranteeId { get; private set; } = default!;

    public MediaAccessLevel Level { get; private set; }

    /// <summary>When the grant lapses. Null means it lasts until revoked.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public UserId GrantedBy { get; private set; } = default!;

    public DateTimeOffset GrantedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    private MediaGrant() { }

    private MediaGrant(
        MediaGrantId id,
        MediaId mediaId,
        UserId granteeId,
        MediaAccessLevel level,
        DateTimeOffset? expiresAt,
        UserId grantedBy) : base(id)
    {
        MediaId = mediaId;
        GranteeId = granteeId;
        Level = level;
        ExpiresAt = expiresAt;
        GrantedBy = grantedBy;
        GrantedAt = DateTimeOffset.UtcNow;
    }

    internal static MediaGrant Create(
        MediaId mediaId,
        UserId granteeId,
        MediaAccessLevel level,
        DateTimeOffset? expiresAt,
        UserId grantedBy)
    {
        ArgumentNullException.ThrowIfNull(mediaId);
        ArgumentNullException.ThrowIfNull(granteeId);
        ArgumentNullException.ThrowIfNull(grantedBy);

        return new MediaGrant(MediaGrantId.New(), mediaId, granteeId, level, expiresAt, grantedBy);
    }

    internal void Amend(MediaAccessLevel level, DateTimeOffset? expiresAt)
    {
        Level = level;
        ExpiresAt = expiresAt;
        RevokedAt = null;
    }

    internal void Revoke() => RevokedAt ??= DateTimeOffset.UtcNow;

    /// <summary>Whether this grant lets <paramref name="caller"/> do <paramref name="level"/>.</summary>
    public bool Permits(UserId caller, MediaAccessLevel level, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(caller);

        return GranteeId == caller &&
               !IsRevoked &&
               (ExpiresAt is null || ExpiresAt > asOf) &&
               Level >= level;
    }

    public bool PermitsRead(UserId caller, DateTimeOffset asOf) =>
        Permits(caller, MediaAccessLevel.Read, asOf);
}
