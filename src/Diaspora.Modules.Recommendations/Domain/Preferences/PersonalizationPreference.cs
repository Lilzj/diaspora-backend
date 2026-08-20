using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.Preferences;

/// <summary>
/// What a member actually asked for.
/// <para>
/// <b>Separate from the profile because one is stated and the other inferred, and a rebuild must
/// never confuse them.</b> <see cref="Profiles.PersonalizationProfile"/> is recomputed from signals
/// and can be dropped entirely without loss; this cannot be recomputed from anything, and dropping
/// it would silently re-enable a category the member muted and re-personalise a surface they opted
/// out of. Keeping them in one aggregate would mean one careless rebuild away from that.
/// </para>
/// <para>
/// <b><see cref="IsPersonalizationEnabled"/> is the master switch, and turning it off is honoured
/// absolutely.</b> Not "personalise less" — the profile stops being consulted, every surface falls
/// back to the cold-start path, and the signal recorder stops writing behavioural rows. A member who
/// says no gets a platform that behaves as though it has never seen them, which is the only reading
/// of "no" worth implementing.
/// </para>
/// <para>
/// <b>Absence means the default, and the default is on.</b> There is no row here for a member who
/// has never opened the settings screen, so every row in this table represents a decision somebody
/// made. That is what makes an audit of it meaningful.
/// </para>
/// </summary>
public sealed class PersonalizationPreference
    : AggregateRoot<PersonalizationPreferenceId>, IAuditableEntity
{
    /// <summary>How many categories one member may mute. Beyond this they want a different product.</summary>
    public const int MaximumCategoryPreferences = 200;

    /// <summary>How many targets one member may block outright.</summary>
    public const int MaximumBlockedTargets = 500;

    /// <summary>How many places one member may favour.</summary>
    public const int MaximumFavoriteLocations = 25;

    private readonly List<CategoryPreference> _categories = [];
    private readonly List<BlockedRecommendationTarget> _blockedTargets = [];
    private readonly List<SurfacePreference> _surfaces = [];
    private readonly List<FavoriteLocation> _favoriteLocations = [];

    private PersonalizationPreference(
        PersonalizationPreferenceId id, UserId userId, DateTimeOffset createdAt) : base(id)
    {
        UserId = userId;
        CreatedAt = createdAt;
        IsPersonalizationEnabled = true;
        ShareActivitySignals = true;
    }

    private PersonalizationPreference() { }

    public UserId UserId { get; private set; } = default!;

    /// <summary>The master switch. See the class summary — off means off.</summary>
    public bool IsPersonalizationEnabled { get; private set; }

    /// <summary>
    /// Whether behavioural signals may be recorded at all.
    /// <para>
    /// Narrower than the master switch and separately useful: a member may be happy for the platform
    /// to use what they said and unhappy for it to record what they looked at.
    /// </para>
    /// </summary>
    public bool ShareActivitySignals { get; private set; }

    /// <summary>The language the member asked to be recommended in. Overrides the profile's.</summary>
    public string? PreferredLanguage { get; private set; }

    /// <summary>
    /// A country the member asked to see content for, when it is not where they are.
    /// <para>
    /// The intended-mover case, and the reason it is a stated preference rather than a derived one:
    /// somebody researching Canada from Lagos has no Canadian signals to derive anything from.
    /// </para>
    /// </summary>
    public string? PreferredCountryCode { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public IReadOnlyList<CategoryPreference> Categories => _categories.AsReadOnly();

    public IReadOnlyList<BlockedRecommendationTarget> BlockedTargets => _blockedTargets.AsReadOnly();

    public IReadOnlyList<SurfacePreference> Surfaces => _surfaces.AsReadOnly();

    public IReadOnlyList<FavoriteLocation> FavoriteLocations => _favoriteLocations.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static PersonalizationPreference Open(
        PersonalizationPreferenceId id, UserId userId, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(userId);

        return new PersonalizationPreference(id, userId, createdAt);
    }

    public void SetPersonalizationEnabled(bool enabled)
    {
        IsPersonalizationEnabled = enabled;
        Touch();
    }

    public void SetShareActivitySignals(bool share)
    {
        ShareActivitySignals = share;
        Touch();
    }

    public void SetPreferredLanguage(string? languageCode)
    {
        PreferredLanguage = string.IsNullOrWhiteSpace(languageCode)
            ? null
            : languageCode.Trim().ToLowerInvariant();

        Touch();
    }

    public void SetPreferredCountry(string? countryCode)
    {
        PreferredCountryCode = string.IsNullOrWhiteSpace(countryCode)
            ? null
            : countryCode.Trim().ToUpperInvariant();

        Touch();
    }

    /// <summary>Mutes or prefers a category, replacing whatever was there before.</summary>
    public void SetCategory(
        string categoryKey, bool isMuted, RecommendationTargetType? targetType, Guid preferenceId)
    {
        if (string.IsNullOrWhiteSpace(categoryKey))
        {
            return;
        }

        var normalized = categoryKey.Trim().ToLowerInvariant();
        var existing = FindCategory(normalized, targetType);

        if (existing is not null)
        {
            existing.SetMuted(isMuted);
            Touch();
            return;
        }

        if (_categories.Count >= MaximumCategoryPreferences)
        {
            return;
        }

        _categories.Add(CategoryPreference.Create(preferenceId, normalized, isMuted, targetType));
        Touch();
    }

    public void ClearCategory(string categoryKey, RecommendationTargetType? targetType)
    {
        if (string.IsNullOrWhiteSpace(categoryKey))
        {
            return;
        }

        var normalized = categoryKey.Trim().ToLowerInvariant();
        var existing = FindCategory(normalized, targetType);

        if (existing is null)
        {
            return;
        }

        _categories.Remove(existing);
        Touch();
    }

    /// <summary>Blocks a target permanently. Idempotent — blocking twice is not an error.</summary>
    public void BlockTarget(
        RecommendationTargetType targetType, Guid targetId, DateTimeOffset blockedAt, Guid blockId)
    {
        if (targetId == Guid.Empty)
        {
            return;
        }

        if (IsBlocked(targetType, targetId))
        {
            return;
        }

        if (_blockedTargets.Count >= MaximumBlockedTargets)
        {
            return;
        }

        _blockedTargets.Add(
            BlockedRecommendationTarget.Create(blockId, targetType, targetId, blockedAt));

        Touch();
    }

    public void UnblockTarget(RecommendationTargetType targetType, Guid targetId)
    {
        var existing = _blockedTargets.Find(block =>
            block.TargetType == targetType && block.TargetId == targetId);

        if (existing is null)
        {
            return;
        }

        _blockedTargets.Remove(existing);
        Touch();
    }

    public void SetSurfaceEnabled(RecommendationSurface surface, bool enabled, Guid preferenceId)
    {
        var existing = _surfaces.Find(entry => entry.Surface == surface);

        if (existing is not null)
        {
            existing.SetEnabled(enabled);
            Touch();
            return;
        }

        _surfaces.Add(SurfacePreference.Create(preferenceId, surface, enabled));
        Touch();
    }

    public void AddFavoriteLocation(
        AffinityScope scope, string key, string? displayName, Guid locationId)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var normalized = scope == AffinityScope.Country
            ? key.Trim().ToUpperInvariant()
            : key.Trim().ToLowerInvariant();

        if (_favoriteLocations.Exists(location =>
                location.Scope == scope &&
                string.Equals(location.Key, normalized, StringComparison.Ordinal)))
        {
            return;
        }

        if (_favoriteLocations.Count >= MaximumFavoriteLocations)
        {
            return;
        }

        _favoriteLocations.Add(FavoriteLocation.Create(locationId, scope, normalized, displayName));
        Touch();
    }

    public void RemoveFavoriteLocation(AffinityScope scope, string key)
    {
        var normalized = scope == AffinityScope.Country
            ? key.Trim().ToUpperInvariant()
            : key.Trim().ToLowerInvariant();

        var existing = _favoriteLocations.Find(location =>
            location.Scope == scope &&
            string.Equals(location.Key, normalized, StringComparison.Ordinal));

        if (existing is null)
        {
            return;
        }

        _favoriteLocations.Remove(existing);
        Touch();
    }

    // ── Reading, for the policies ───────────────────────────────────────────────────────────

    /// <summary>
    /// Whether a category is muted, at the target type or across the platform.
    /// <para>
    /// A platform-wide mute wins over a per-type preference, which is the reading a member expects:
    /// somebody who muted "politics" everywhere did not mean "except in events".
    /// </para>
    /// </summary>
    public bool IsCategoryMuted(string categoryKey, RecommendationTargetType targetType)
    {
        if (string.IsNullOrWhiteSpace(categoryKey))
        {
            return false;
        }

        var normalized = categoryKey.Trim().ToLowerInvariant();

        return _categories.Exists(preference =>
            preference.IsMuted &&
            string.Equals(preference.CategoryKey, normalized, StringComparison.Ordinal) &&
            (preference.TargetType is null || preference.TargetType == targetType));
    }

    public bool IsCategoryPreferred(string categoryKey, RecommendationTargetType targetType)
    {
        if (string.IsNullOrWhiteSpace(categoryKey))
        {
            return false;
        }

        var normalized = categoryKey.Trim().ToLowerInvariant();

        return _categories.Exists(preference =>
            !preference.IsMuted &&
            string.Equals(preference.CategoryKey, normalized, StringComparison.Ordinal) &&
            (preference.TargetType is null || preference.TargetType == targetType));
    }

    public bool IsBlocked(RecommendationTargetType targetType, Guid targetId) =>
        _blockedTargets.Exists(block => block.TargetType == targetType && block.TargetId == targetId);

    /// <summary>Absence means enabled. See the class summary.</summary>
    public bool IsSurfaceEnabled(RecommendationSurface surface) =>
        _surfaces.Find(entry => entry.Surface == surface)?.IsEnabled ?? true;

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

    private CategoryPreference? FindCategory(
        string normalizedKey, RecommendationTargetType? targetType) =>
        _categories.Find(preference =>
            string.Equals(preference.CategoryKey, normalizedKey, StringComparison.Ordinal) &&
            preference.TargetType == targetType);

    private void Touch() => Revision++;
}
