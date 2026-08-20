using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Guides.V1.Events;

/// <summary>
/// Somebody commented on a guide.
/// <para>
/// Relayed from Community's <c>CommunityCommentAddedIntegrationEvent</c>, filtered to guide
/// content, so a guide-focused consumer subscribes to this instead of sifting the platform-wide
/// comment stream for a matching content type. Community remains the owner of the comment — its
/// event is the general fact, this one is the guide-shaped convenience — and a consumer that
/// already listens to Community's stream should not also subscribe here, or it counts every
/// comment twice.
/// </para>
/// </summary>
public sealed record GuideCommentAddedIntegrationEvent : IntegrationEvent
{
    public required Guid GuideId { get; init; }

    public required Guid CommentId { get; init; }

    /// <summary>
    /// Who wrote the <b>comment</b>, not who wrote the guide.
    /// <para>
    /// <b>Read this together with <see cref="AuthorUserId"/>, which is the opposite person.</b>
    /// The two names are one letter apart and name the two sides of a conversation; a consumer
    /// that confuses them tells somebody their own comment arrived. The names were not
    /// harmonised in B13.1 because <c>AuthorId</c> is a published property and renaming one
    /// breaks every deserialiser in the estate.
    /// </para>
    /// </summary>
    public required Guid AuthorId { get; init; }

    public required DateTimeOffset AddedAt { get; init; }

    /// <summary>
    /// The member who wrote the <b>guide</b> — B13.1. The recipient of "somebody replied to
    /// your guide", and not <see cref="AuthorId"/>, which is the commenter.
    /// <para>Null on events published before B13.1, and null when the guide could not be read.</para>
    /// </summary>
    public Guid? AuthorUserId { get; init; }
}
