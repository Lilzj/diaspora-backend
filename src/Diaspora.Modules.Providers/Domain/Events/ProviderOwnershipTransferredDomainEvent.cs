using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Events;

/// <summary>
/// Raised when a provider changes hands.
/// <para>
/// Its own event rather than a pair of role changes, because it is the single most
/// consequential act in the module — a business is sold, a pastor retires, an association
/// elects a new chair — and every one of audit, notification and fraud review wants to see it as
/// one thing that happened rather than as two coincidental role edits.
/// </para>
/// </summary>
public sealed record ProviderOwnershipTransferredDomainEvent : DomainEvent
{
    public required ProviderId ProviderId { get; init; }

    public required UserId PreviousOwnerId { get; init; }

    public required UserId NewOwnerId { get; init; }

    /// <summary>Whether the previous owner kept a lesser role rather than leaving entirely.</summary>
    public required bool PreviousOwnerRetained { get; init; }
}
