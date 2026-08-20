namespace Diaspora.Contracts.Authorization;

/// <summary>
/// One permission, declared by the module that owns the capability it protects.
/// <para>
/// A definition is data, not code: the platform can enumerate the whole catalogue, render
/// it into documentation, diff it between releases, and answer "who can do this?" without
/// executing anything. That is the difference between this and the hardcoded resolver it
/// replaces.
/// </para>
/// <para>
/// <b>A key is a public contract from the moment it ships.</b> It is minted into access tokens,
/// held in the frontend's capability map and stored in issued sessions, so a key can be added
/// freely and renamed only under a versioned authorisation release. Everything else on this
/// record — the tier, the roles, the audience, the description — is backend state and may change
/// whenever the rule it expresses changes.
/// </para>
/// <para>
/// <b>The full contributor reference is <c>docs/platform/permission-guide.md</c>:</b> naming, the
/// lifecycle from declaration to token, what a module is expected to declare, how a permission
/// relates to a feature gate and to a role, and how the two authorisation layers use it.
/// </para>
/// </summary>
/// <param name="Key">
/// The permission string authorisation checks name. Colon-separated, lower case, unique across
/// the platform, and <b>two segments or more</b> — the resource segment is written when it
/// distinguishes something and left out when it does not:
/// <list type="bullet">
///   <item><c>module:action</c> — <c>bookmarks:read</c>, <c>content:publish</c>. Correct, and the
///   shape of eighty-eight shipped keys. A module with one thing to protect gains nothing by
///   naming it twice.</item>
///   <item><c>module:resource:action</c> — <c>marketplace:listings:create</c>. The most common
///   shape, and the one to reach for when a module protects several kinds of thing.</item>
///   <item><c>module:resource:action:scope</c> — <c>community-life:event:edit:own</c>. A fourth
///   segment separates authority over your own rows from authority over anybody's, which is a
///   different permission and not a runtime check.</item>
/// </list>
/// <b>This comment used to say <c>module:resource:action</c> and only that</b>, which disagreed
/// with the shipped catalogue eighty-eight times. B1.4 recorded the divergence as F4 and B1.4.1
/// corrected the comment rather than the keys: a key is minted into access tokens and held in the
/// frontend's capability map, so the catalogue was right and the documentation was wrong.
/// <c>DSP5002</c> enforces what is written here.
/// <para>
/// Segments are lower case and may contain hyphens (<c>identity:security-events:read</c>). The
/// action segment uses the platform's vocabulary rather than a synonym — <c>read</c>, not
/// <c>view</c>, <c>list</c> or <c>browse</c>; <c>create</c>, not <c>add</c>. <c>DSP5004</c> says
/// so as the key is typed. See <c>docs/platform/permission-guide.md</c>.
/// </para>
/// </param>
/// <param name="Module">The owning module. Used for grouping and for conflict messages.</param>
/// <param name="Description">
/// Human-readable purpose, surfaced in the platform manifest. Not optional in practice:
/// <c>SV3002</c> reports an undescribed permission, because the manifest publishes the catalogue
/// and an undescribed key is one nobody outside its author can answer "who can do this?" for.
/// </param>
public sealed record PermissionDefinition(string Key, string Module, string Description)
{
    /// <summary>
    /// The lowest tier that holds this permission. <c>null</c> means no tier grants it —
    /// use that for machine-to-machine permissions no human role should ever receive.
    /// </summary>
    public AccessTier? MinimumTier { get; init; } = AccessTier.Limited;

    /// <summary>
    /// The highest tier that holds this permission, when a capability stops applying as
    /// the member progresses. <c>identity:verification:submit</c> uses this: once a request
    /// is with the Trust Engine, submitting again is not a thing the member can do.
    /// </summary>
    public AccessTier? MaximumTier { get; init; }

    /// <summary>
    /// Roles that hold this permission. Empty means every role at or above
    /// <see cref="MinimumTier"/> holds it; a non-empty set restricts it to those roles.
    /// </summary>
    public IReadOnlySet<string> Roles { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Whether the permission survives suspension, lock or closure. Almost nothing should:
    /// the exception is reading your own profile, so the screen explaining the restriction
    /// can address you by name.
    /// </summary>
    public bool GrantedWhenRestricted { get; init; }

    /// <summary>
    /// Who this permission is issued to. Service permissions are never resolved from a
    /// member's tier or role — they reach a token only through
    /// <c>IServiceTokenIssuer</c>, which is what keeps the Trust Engine's authority out of
    /// every human role.
    /// </summary>
    public PermissionAudience Audience { get; init; } = PermissionAudience.Member;
}
