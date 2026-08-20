using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Media.Domain.Events;

/// <summary>Raised when every part has arrived and the session has produced an asset.</summary>
public sealed record UploadSessionCompletedDomainEvent : DomainEvent
{
    public required UploadSessionId SessionId { get; init; }

    public required MediaId MediaId { get; init; }

    public required int ChunkCount { get; init; }
}
