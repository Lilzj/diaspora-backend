using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Providers.Extensibility.Availability;

/// <summary>
/// The question an availability provider is asked.
/// <para>
/// Carries the aggregate rather than a projection of it, unlike
/// <c>ProviderVerificationRequest</c>, and the asymmetry is deliberate: a verification provider
/// talks to an external register and needs only identifiers, whereas an availability provider
/// needs the schedule, the exceptions, the delivery mode and the services — most of the
/// aggregate — and a flattened copy would be a second model to keep in step. The provider is
/// given a loaded aggregate and is expected not to mutate it.
/// </para>
/// </summary>
public sealed record AvailabilityQuery
{
    public required Provider Provider { get; init; }

    /// <summary>The instant being asked about. Usually now; a future one for a planner.</summary>
    public required DateTimeOffset At { get; init; }

    /// <summary>The caller, for a provider that answers differently for a member with a booking.</summary>
    public UserId? ViewerId { get; init; }

    /// <summary>A particular service, when the question is about one rather than the provider.</summary>
    public Domain.Identifiers.ProviderServiceId? ServiceId { get; init; }
}
