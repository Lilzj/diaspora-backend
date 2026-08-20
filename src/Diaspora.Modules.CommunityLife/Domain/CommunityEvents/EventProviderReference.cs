using Diaspora.Modules.CommunityLife.Domain.Policies;

namespace Diaspora.Modules.CommunityLife.Domain.CommunityEvents;

/// <summary>
/// A B7 provider involved in an event — the training company running the workshop, the caterer, the
/// venue.
/// <para>
/// This is how the brief's provider requirement is met: a verified provider hosts a conference by
/// being named here, and B7 keeps every fact about the provider itself. The name is cached for
/// rendering; the role is the organizer's own word for what the provider is doing.
/// </para>
/// </summary>
public sealed class EventProviderReference
{
    private EventProviderReference(Guid providerId, string? providerName, string? role)
    {
        ProviderId = providerId;
        ProviderName = providerName;
        Role = role;
    }

    private EventProviderReference() { }

    public Guid ProviderId { get; private set; }

    public string? ProviderName { get; private set; }

    /// <summary>What they are doing: Host, Venue, Caterer, Sponsor.</summary>
    public string? Role { get; private set; }

    public static EventProviderReference Create(
        Guid providerId, string? providerName, string? role) =>
        new(
            providerId,
            Clip(providerName, CommunityLifeLimits.MaxDisplayName),
            Clip(role, CommunityLifeLimits.MaxDisplayName));

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }
}
