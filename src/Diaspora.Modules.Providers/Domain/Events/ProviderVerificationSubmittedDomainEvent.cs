using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Events;

/// <summary>
/// Raised when a provider asks Trust to verify something about it.
/// <para>
/// Says only that a claim was made. It carries no outcome and must never be read as one — the
/// outcome arrives later as <see cref="ProviderVerificationChangedDomainEvent"/>, raised from a
/// Trust integration event. ADR-002's separation, applied to providers.
/// </para>
/// </summary>
public sealed record ProviderVerificationSubmittedDomainEvent : DomainEvent
{
    public required ProviderId ProviderId { get; init; }

    public required ProviderVerificationKind Kind { get; init; }

    /// <summary>Trust's own request identifier, for reconciling the two systems.</summary>
    public required Guid TrustVerificationId { get; init; }

    public required UserId SubmittedBy { get; init; }
}
