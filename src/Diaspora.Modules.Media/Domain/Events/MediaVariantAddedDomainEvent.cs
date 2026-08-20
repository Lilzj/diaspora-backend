using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Media.Domain.Events;

/// <summary>Raised when a derived rendition of an asset has been produced and stored.</summary>
public sealed record MediaVariantAddedDomainEvent : DomainEvent
{
    public required MediaId MediaId { get; init; }

    public required MediaVariantPurpose Purpose { get; init; }

    public required long SizeInBytes { get; init; }
}
