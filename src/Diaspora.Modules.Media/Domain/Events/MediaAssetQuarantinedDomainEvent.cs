using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Media.Domain.Events;

/// <summary>
/// Raised when a scanner flagged an asset.
/// <para>
/// Carries the uploader deliberately. A member who uploads malware is a signal Trust wants —
/// once may be an infected phone, repeatedly is not — and the alternative, having Trust poll
/// Media for quarantines, would put a Media query in a Trust loop.
/// </para>
/// </summary>
public sealed record MediaAssetQuarantinedDomainEvent : DomainEvent
{
    public required MediaId MediaId { get; init; }

    public required UserId UploadedBy { get; init; }

    public required string Scanner { get; init; }

    public string? ThreatName { get; init; }
}
