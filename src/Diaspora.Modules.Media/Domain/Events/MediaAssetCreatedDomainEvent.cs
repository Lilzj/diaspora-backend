using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Media.Domain.Events;

/// <summary>Raised when bytes have landed and an asset record exists for them.</summary>
public sealed record MediaAssetCreatedDomainEvent : DomainEvent
{
    public required MediaId MediaId { get; init; }

    public required UserId UploadedBy { get; init; }

    public required MediaKind Kind { get; init; }

    public required long SizeInBytes { get; init; }

    public required string ContentType { get; init; }
}
