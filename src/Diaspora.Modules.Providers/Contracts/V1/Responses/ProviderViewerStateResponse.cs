namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// What this particular caller may do with this particular provider.
/// <para>
/// <b>Computed server-side rather than inferred by the client.</b> The client's provider screen
/// already needs three of these answers — it greys out "Call" and "Message" for an owner, it
/// fills or empties the save heart, and it decides whether to offer "Write a Review" — and it
/// currently derives them by comparing identifiers it happens to have. That works until the
/// rules grow a second clause, at which point every client derives them slightly differently.
/// </para>
/// <para>
/// <see cref="CanReview"/> is the clearest example: a member may not review a provider they are
/// attached to, may not review twice, and may edit only inside the edit window. Three rules the
/// server already enforces, and a client that guesses them wrong shows a form that 409s on
/// submit.
/// </para>
/// </summary>
public sealed record ProviderViewerStateResponse
{
    /// <summary>The caller's role, lower-case, or null when they hold none.</summary>
    public string? Role { get; init; }

    /// <summary>Whether the caller may edit descriptive content.</summary>
    public required bool CanManage { get; init; }

    /// <summary>Whether the caller may add and remove people.</summary>
    public required bool CanManageMembers { get; init; }

    /// <summary>Whether the caller may publish, pause or close.</summary>
    public required bool CanManageStatus { get; init; }

    /// <summary>Whether a review form should be offered. False for staff, and after reviewing.</summary>
    public required bool CanReview { get; init; }

    /// <summary>Whether the caller may still edit the review they wrote.</summary>
    public required bool CanEditOwnReview { get; init; }

    /// <summary>The heart on the client's provider screen. Sourced from Community's bookmarks.</summary>
    public required bool IsBookmarked { get; init; }

    /// <summary>Sourced from Community's follows.</summary>
    public required bool IsFollowing { get; init; }

    /// <summary>The caller's own review, when they have written one.</summary>
    public Guid? OwnReviewId { get; init; }

    /// <summary>Whether the caller has already recommended this provider.</summary>
    public required bool HasRecommended { get; init; }
}
