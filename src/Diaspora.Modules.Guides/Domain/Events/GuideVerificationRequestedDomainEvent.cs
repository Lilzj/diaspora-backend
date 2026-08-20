using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>
/// Raised when a guide's verification claim goes to Trust. Says nothing about the outcome —
/// that arrives later, from Trust, as <see cref="GuideVerificationChangedDomainEvent"/>.
/// </summary>
public sealed record GuideVerificationRequestedDomainEvent : DomainEvent
{
    public required GuideId GuideId { get; init; }

    public required UserId RequestedBy { get; init; }

    public required DateTimeOffset RequestedAt { get; init; }
}
