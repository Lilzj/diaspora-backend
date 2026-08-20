using Diaspora.Modules.CommunityLife.Domain.Policies;

namespace Diaspora.Modules.CommunityLife.Domain.Organizations;

/// <summary>
/// Something an organization does for people — food bank, legal advice, homework club.
/// <para>
/// Free text rather than a catalogue, and that is the difference between this and B7's service
/// categories. A provider's category is a taxonomy the platform maintains because members search
/// across it; what a community organization offers is written by the organization, varies wildly,
/// and is rendered as chips rather than filtered on.
/// </para>
/// </summary>
public sealed class OrganizationService
{
    private OrganizationService(string name, int order)
    {
        Name = name;
        Order = order;
    }

    private OrganizationService() { }

    public string Name { get; private set; } = string.Empty;

    public int Order { get; private set; }

    public static OrganizationService Create(string? name, int order = 0)
    {
        var trimmed = name?.Trim() ?? string.Empty;

        if (trimmed.Length > CommunityLifeLimits.MaxServiceName)
        {
            trimmed = trimmed[..CommunityLifeLimits.MaxServiceName];
        }

        return new OrganizationService(trimmed, Math.Max(0, order));
    }
}
