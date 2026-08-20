using Diaspora.Modules.Providers.Domain.Enums;

namespace Diaspora.Modules.Providers.Extensibility.Types;

/// <summary>
/// What one kind of provider is called, and what it must supply.
/// <para>
/// The registration wizard is built from these: a client asks the capabilities endpoint which
/// types exist, renders them with their labels and icons, and knows before submitting that
/// choosing "Business" will ask for a registration number. Hard-coding that list in the client —
/// which is what the frontend does today with <c>OrgCategory</c> — means every deployment gets
/// the same list and a new type needs an app release.
/// </para>
/// </summary>
public sealed record ProviderTypeDescriptor
{
    public required ProviderType Type { get; init; }

    /// <summary>What the registration wizard calls it. "Business", "Church or mosque".</summary>
    public required string Label { get; init; }

    /// <summary>One line explaining who should choose this. Rendered under the label.</summary>
    public string? Description { get; init; }

    /// <summary>An emoji or icon name. Opaque to B7.</summary>
    public string? Icon { get; init; }

    /// <summary>Whether this deployment offers the type at all.</summary>
    public bool IsAvailable { get; init; } = true;

    /// <summary>
    /// Whether registration detail is required to publish. True for a regulated market's
    /// healthcare providers; false for an individual anywhere.
    /// </summary>
    public bool RequiresRegistrationNumber { get; init; }

    /// <summary>
    /// Whether the type is expected to carry organizational detail at all. False for
    /// <see cref="ProviderType.Individual"/>, which is what stops the wizard asking a braider
    /// for her company number.
    /// </summary>
    public bool SupportsOrganizationDetail { get; init; } = true;

    /// <summary>
    /// The verification kinds that make sense for this type. A church is verified as an
    /// organization, not as a business; showing a "verify business registration" button on a
    /// mosque's dashboard is how a product tells a community it was not designed for them.
    /// </summary>
    public IReadOnlyList<ProviderVerificationKind> VerificationKinds { get; init; } = [];

    /// <summary>The default delivery mode the wizard pre-selects.</summary>
    public ServiceDeliveryMode DefaultDeliveryMode { get; init; } = ServiceDeliveryMode.OnSite;
}
