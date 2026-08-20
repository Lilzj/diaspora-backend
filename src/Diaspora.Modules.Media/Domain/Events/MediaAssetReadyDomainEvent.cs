using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Media.Domain.Events;

/// <summary>
/// Raised when an asset has passed validation and scanning and may be served.
/// <para>
/// The event a consuming module waits for. A vertical that publishes a listing the moment the
/// upload returns will publish a listing whose photo is still being scanned; subscribing to
/// this instead means the listing goes live when its photo is genuinely readable.
/// </para>
/// </summary>
public sealed record MediaAssetReadyDomainEvent : DomainEvent
{
    public required MediaId MediaId { get; init; }

    public required MediaKind Kind { get; init; }

    public required int VariantCount { get; init; }
}
