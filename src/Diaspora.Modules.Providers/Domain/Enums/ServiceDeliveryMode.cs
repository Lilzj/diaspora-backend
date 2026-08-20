namespace Diaspora.Modules.Providers.Domain.Enums;

/// <summary>
/// Where the service actually happens.
/// <para>
/// Replaces the client's <c>location.serviceType: 'Fixed'</c>, which has no antonym and
/// therefore no way to express the three cases the brief requires: remote services, hybrid
/// services and providers who travel to the customer. A mobile hairdresser and a barbershop
/// are both "Fixed" today, and the difference is the entire question a member is asking.
/// </para>
/// <para>
/// This drives service-area semantics: an <see cref="OnSite"/> provider is found by proximity
/// to its own location, an <see cref="AtCustomer"/> provider by whether the member falls inside
/// one of its <see cref="Providers.ProviderServiceArea"/> entries, and a <see cref="Remote"/>
/// provider by neither — which is why a remote provider that is excluded from a radius search
/// is a bug and not a filter working correctly.
/// </para>
/// </summary>
public enum ServiceDeliveryMode
{
    /// <summary>At the provider's own premises. A restaurant, a clinic, a church.</summary>
    OnSite = 1,

    /// <summary>At the customer's address. A mobile hairdresser, a plumber, a tutor.</summary>
    AtCustomer = 2,

    /// <summary>Online or by phone, with no geography at all. An immigration adviser.</summary>
    Remote = 3,

    /// <summary>Any combination of the above. The honest answer for most consultancies.</summary>
    Hybrid = 4
}
