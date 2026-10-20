namespace Diaspora.Modules.CommunityProgramme.Contracts.V1.Requests;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// A community's rules and notices, and a member's preferences — 21 Oct 2026.
//
// Shaped to `src/types/community-directory.ts` in the app, field for field. Every field is nullable
// so a missing one reaches the domain and is refused there with a sentence, rather than at model
// binding with a 400 nobody can read.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>A rule, as the editor sends it. Creating and replacing take the same body.</summary>
public sealed record SaveCommunityRuleRequest
{
    public string? Title { get; init; }

    public string? Body { get; init; }

    /// <summary>What happens to somebody who breaks it. Optional.</summary>
    public string? Consequence { get; init; }

    /// <summary>
    /// One-based place in the list. Omitted on create, the rule goes last; omitted on replace, it
    /// stays where it is. Out-of-range values are clamped to the list.
    /// </summary>
    public int? Position { get; init; }
}

/// <summary>A notice, as the publisher sends it. Publishing and revising take the same body.</summary>
public sealed record SaveCommunityNoticeRequest
{
    public string? Title { get; init; }

    public string? Body { get; init; }

    /// <summary>When it stops being shown. Omitted, it stays until removed.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Whether it leads the list.</summary>
    public bool? IsPinned { get; init; }
}

/// <summary>One row of the notification overlay — <c>CommunityNotificationPreference</c>.</summary>
public sealed record CommunityNotificationPreferenceRequest
{
    public string? CategoryKey { get; init; }

    /// <summary>primary-only, my-communities, interested-included, all-communities, specific or none.</summary>
    public string? Scope { get; init; }

    public IReadOnlyList<Guid>? CommunityIds { get; init; }
}

/// <summary>
/// The member's community preferences — the app's <c>CommunityPreferences</c>, plus the one field it
/// does not yet send. A field that is sent replaces what is stored; a field that is omitted keeps it,
/// so a client that knows nothing of a field cannot reset it. The app sends the whole record.
/// </summary>
public sealed record SaveCommunityPreferencesRequest
{
    public bool? CrossCommunityDiscoveryEnabled { get; init; }

    public bool? ShowPrimaryCommunityOnProfile { get; init; }

    public bool? ShowInterestedCommunitiesOnProfile { get; init; }

    public bool? ShowCommunityRolesOnProfile { get; init; }

    public IReadOnlyList<CommunityNotificationPreferenceRequest>? Notifications { get; init; }

    /// <summary>BCP-47, or null for no preference. The one field always replaced: null clears it.</summary>
    public string? PreferredContentLanguage { get; init; }

    /// <summary>
    /// Private, CommunityOnly or Public: where a new relationship with a community starts. Omitted,
    /// the stored value is kept — the app's current settings screen does not send it.
    /// </summary>
    public string? DefaultMembershipVisibility { get; init; }
}
