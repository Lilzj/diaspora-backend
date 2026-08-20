namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// One member's rated review of a provider.
/// <para>
/// The client's <c>mockReviews</c> element, with <c>listingId</c> renamed to
/// <c>providerId</c> and four fields it does not have: the service reviewed, the provider's
/// reply, whether the interaction was verified, and whether the review has been edited.
/// </para>
/// <para>
/// <see cref="IsVerifiedInteraction"/> is not a gate and must not be rendered as one. Most
/// business on this platform happens by phone and leaves no trace, so an unverified review is
/// the normal case rather than a suspicious one — it is a ranking and trust signal, not a
/// badge of authenticity whose absence implies the opposite.
/// </para>
/// </summary>
public sealed record ProviderReviewResponse
{
    public required Guid ReviewId { get; init; }

    public required Guid ProviderId { get; init; }

    public required Guid ReviewerId { get; init; }

    /// <summary>Null when the caller may not see it, or when Identity has no record.</summary>
    public string? ReviewerName { get; init; }

    /// <summary>One to five.</summary>
    public required int Rating { get; init; }

    public string? Title { get; init; }

    public string? Body { get; init; }

    /// <summary>The <c>ReviewStatus</c> name.</summary>
    public required string Status { get; init; }

    /// <summary>The service reviewed, when the member named one.</summary>
    public Guid? ServiceId { get; init; }

    /// <summary>The provider's public reply. One, not a thread.</summary>
    public string? Response { get; init; }

    public DateTimeOffset? RespondedAt { get; init; }

    /// <summary>A signal, not a gate. See the type's own note.</summary>
    public required bool IsVerifiedInteraction { get; init; }

    /// <summary>Projected from Community's reactions.</summary>
    public required int HelpfulCount { get; init; }

    public required bool IsEdited { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? EditedAt { get; init; }
}
