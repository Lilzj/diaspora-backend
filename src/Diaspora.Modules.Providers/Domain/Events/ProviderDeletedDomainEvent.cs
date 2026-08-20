using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Events;

/// <summary>
/// Raised when a provider is soft-deleted.
/// <para>
/// Soft, because the reviews members wrote about it are theirs and outlive the provider, and
/// because a restore has to be possible after an accidental deletion or a successful appeal. The
/// row survives; every read path filters it out.
/// </para>
/// <para>
/// Consumers must treat this as a removal, not a pause: a search index that keeps the document
/// serves a provider that no longer exists, and a member who taps it lands on a 404.
/// </para>
/// </summary>
public sealed record ProviderDeletedDomainEvent : DomainEvent
{
    public required ProviderId ProviderId { get; init; }

    public required UserId DeletedBy { get; init; }

    public string? Reason { get; init; }
}
