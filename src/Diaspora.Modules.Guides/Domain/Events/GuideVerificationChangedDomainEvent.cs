using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>Raised when Trust's decision changes the guide's trust mark.</summary>
public sealed record GuideVerificationChangedDomainEvent : DomainEvent
{
    public required GuideId GuideId { get; init; }

    public required GuideVerificationState PreviousState { get; init; }

    public required GuideVerificationState CurrentState { get; init; }

    public Guid? VerificationId { get; init; }

    public string? Badge { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
