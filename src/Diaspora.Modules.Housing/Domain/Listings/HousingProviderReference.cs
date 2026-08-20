using Diaspora.Modules.Housing.Domain.Policies;

namespace Diaspora.Modules.Housing.Domain.Listings;

/// <summary>
/// A B7 provider connected to this property - the managing agent, the relocation company, the
/// student-housing operator.
/// <para>
/// <b>An identifier and a cached name, and nothing else.</b> The provider itself belongs to B7:
/// its services, its opening hours, its verification and its own listings are B7's to own and B7's
/// to change. Housing stores the identifier so a client can follow the link, and the name so a
/// card need not fan out to Providers to render one line.
/// </para>
/// <para>
/// The relationship is a reference rather than ownership because a letting agency manages
/// properties for many landlords and appears on many listings. Modelling it the other way would
/// make an agency's rename a bulk update across every property it touches.
/// </para>
/// </summary>
public sealed class HousingProviderReference
{
    private HousingProviderReference(Guid providerId, string? providerName, string? role)
    {
        ProviderId = providerId;
        ProviderName = providerName;
        Role = role;
    }

    private HousingProviderReference() { }

    /// <summary>B7's identifier. The authoritative half of this reference.</summary>
    public Guid ProviderId { get; private set; }

    /// <summary>Cached for rendering. B7 owns the truth; a rename refreshes this on next write.</summary>
    public string? ProviderName { get; private set; }

    /// <summary>What they do here: managing agent, letting agent, relocation partner.</summary>
    public string? Role { get; private set; }

    public static HousingProviderReference Create(
        Guid providerId, string? providerName, string? role = null) =>
        new(providerId,
            Clamp(providerName, HousingLimits.MaxDisplayName),
            Clamp(role, HousingLimits.MaxDisplayName));

    private static string? Clamp(string? value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > maximum ? trimmed[..maximum] : trimmed;
    }
}
