using System.Text.Json;
using System.Text.RegularExpressions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Domain.Communities;

/// <summary>
/// One row of the member's community notification overlay (§40).
/// <para>
/// <b>An overlay on the Notifications module's categories, never a second notification system.</b>
/// <see cref="CategoryKey"/> is one of that module's category keys and is stored as given; this
/// record only says which communities a category applies to.
/// </para>
/// </summary>
public sealed record CommunityNotificationPreference(
    string CategoryKey, string Scope, IReadOnlyList<Guid> CommunityIds);

/// <summary>
/// A member's community preferences — FRONTEND-PHASE13-GAP-012 and -021, 21 Oct 2026.
/// <para>
/// <b>Every visibility flag is off until the member turns it on.</b> A member's diaspora community
/// can imply their nationality, immigration status, religion or ethnicity, and §41 forbids exposing
/// it without the member's say-so. A member who never opened the settings screen has no row, and
/// the defaults answered in its place are these same off values — absence can never read as consent.
/// </para>
/// <para>
/// <b><see cref="DefaultMembershipVisibility"/> is the one preference the server acts on.</b> A new
/// relationship with a community starts at it rather than at Private. The profile flags are stored
/// for the client to honour; no server surface publishes a member's communities today.
/// </para>
/// </summary>
public sealed partial class CommunityMemberPreferences
    : AggregateRoot<CommunityMemberPreferencesId>, IAuditableEntity
{
    public const int MaximumNotificationRows = 50;
    public const int MaximumCommunitiesPerRow = 50;
    public const int CategoryKeyMaximumLength = 64;
    public const int LanguageMaximumLength = 35;

    /// <summary>The scopes the client's settings screen offers, spelled as it sends them.</summary>
    public static readonly IReadOnlySet<string> Scopes = new HashSet<string>(StringComparer.Ordinal)
    {
        "primary-only", "my-communities", "interested-included", "all-communities", "specific", "none"
    };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private CommunityMemberPreferences(CommunityMemberPreferencesId id, UserId memberId) : base(id)
    {
        MemberId = memberId;
        CrossCommunityDiscoveryEnabled = true;
        DefaultMembershipVisibility = CommunityMembershipVisibility.Private;
        NotificationsJson = "[]";
    }

    // EF Core.
    private CommunityMemberPreferences() { }

    public UserId MemberId { get; private set; } = null!;

    public bool CrossCommunityDiscoveryEnabled { get; private set; }

    public bool ShowPrimaryCommunityOnProfile { get; private set; }

    public bool ShowInterestedCommunitiesOnProfile { get; private set; }

    public bool ShowCommunityRolesOnProfile { get; private set; }

    /// <summary>Where a new relationship with a community starts. Private unless the member said otherwise.</summary>
    public CommunityMembershipVisibility DefaultMembershipVisibility { get; private set; }

    public string? PreferredContentLanguage { get; private set; }

    /// <summary>
    /// The notification overlay, as JSON. A list of small records with no identity of their own that
    /// nothing queries into — a table would buy a join for nothing.
    /// </summary>
    public string NotificationsJson { get; private set; } = "[]";

    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public IReadOnlyList<CommunityNotificationPreference> Notifications =>
        JsonSerializer.Deserialize<List<CommunityNotificationPreference>>(NotificationsJson, Json) ?? [];

    public static CommunityMemberPreferences CreateFor(UserId memberId)
    {
        ArgumentNullException.ThrowIfNull(memberId);
        return new CommunityMemberPreferences(CommunityMemberPreferencesId.New(), memberId);
    }

    /// <summary>
    /// Replaces every preference at once — the settings screen sends the whole record, and a partial
    /// write would let a field the client forgot fall back to whatever was stored before.
    /// </summary>
    public Result Replace(
        bool crossCommunityDiscoveryEnabled,
        bool showPrimaryCommunityOnProfile,
        bool showInterestedCommunitiesOnProfile,
        bool showCommunityRolesOnProfile,
        CommunityMembershipVisibility defaultMembershipVisibility,
        string? preferredContentLanguage,
        IReadOnlyList<CommunityNotificationPreference> notifications)
    {
        ArgumentNullException.ThrowIfNull(notifications);

        if (notifications.Count > MaximumNotificationRows
            || notifications.Any(row => row.CommunityIds.Count > MaximumCommunitiesPerRow))
        {
            return Result.Failure(CommunityGovernanceErrors.PreferencesTooManyNotifications);
        }

        var cleaned = new List<CommunityNotificationPreference>(notifications.Count);

        foreach (var row in notifications)
        {
            var category = row.CategoryKey?.Trim();

            if (string.IsNullOrEmpty(category) || category.Length > CategoryKeyMaximumLength)
            {
                return Result.Failure(CommunityGovernanceErrors.PreferenceCategoryRequired);
            }

            if (!Scopes.Contains(row.Scope ?? string.Empty))
            {
                return Result.Failure(CommunityGovernanceErrors.PreferenceScopeUnknown);
            }

            // Communities are kept only where they mean something: a list under any other scope is
            // ignored by every reader, and storing it would make the record say two things.
            var communities = row.Scope == "specific"
                ? row.CommunityIds.Where(id => id != Guid.Empty).Distinct().ToList()
                : [];

            cleaned.Add(new CommunityNotificationPreference(category, row.Scope!, communities));
        }

        var language = string.IsNullOrWhiteSpace(preferredContentLanguage)
            ? null
            : preferredContentLanguage.Trim();

        if (language is not null
            && (language.Length > LanguageMaximumLength || !LanguageTag().IsMatch(language)))
        {
            return Result.Failure(CommunityGovernanceErrors.PreferenceLanguageShape);
        }

        CrossCommunityDiscoveryEnabled = crossCommunityDiscoveryEnabled;
        ShowPrimaryCommunityOnProfile = showPrimaryCommunityOnProfile;
        ShowInterestedCommunitiesOnProfile = showInterestedCommunitiesOnProfile;
        ShowCommunityRolesOnProfile = showCommunityRolesOnProfile;
        DefaultMembershipVisibility = defaultMembershipVisibility;
        PreferredContentLanguage = language;
        NotificationsJson = JsonSerializer.Serialize(cleaned, Json);
        Revision++;

        return Result.Success();
    }

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

    /// <summary>BCP-47 in the shape a device sends: a primary subtag and up to three more.</summary>
    [GeneratedRegex("^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8}){0,3}$")]
    private static partial Regex LanguageTag();
}
