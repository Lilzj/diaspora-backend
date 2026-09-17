using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Events;

/// <summary>
/// Raised when an author changes how urgent a published alert is, or how far it reaches.
/// <para>
/// Separate from <see cref="PostEditedDomainEvent"/> on purpose: that one means the words changed,
/// and its consumers re-read the body for mentions and tags. An amended urgency or radius changes
/// what a map draws and nothing a reader wrote.
/// </para>
/// </summary>
public sealed record PostAlertAmendedDomainEvent : DomainEvent
{
    public required CommunityPostId PostId { get; init; }

    public required UrgencyLevel Urgency { get; init; }

    public int? AffectedRadiusMetres { get; init; }

    public required DateTimeOffset AmendedAt { get; init; }
}
