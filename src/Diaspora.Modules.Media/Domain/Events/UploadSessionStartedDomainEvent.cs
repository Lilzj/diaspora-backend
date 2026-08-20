using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Media.Domain.Events;

/// <summary>Raised when a member opens an upload.</summary>
public sealed record UploadSessionStartedDomainEvent : DomainEvent
{
    public required UploadSessionId SessionId { get; init; }

    public required UserId StartedBy { get; init; }

    public required MediaKind Kind { get; init; }

    public required UploadProtocol Protocol { get; init; }

    public required long DeclaredSizeInBytes { get; init; }
}
