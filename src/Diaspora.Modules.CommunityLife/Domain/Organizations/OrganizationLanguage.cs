using Diaspora.Modules.CommunityLife.Domain.Policies;

namespace Diaspora.Modules.CommunityLife.Domain.Organizations;

/// <summary>
/// A language you will be understood in.
/// <para>
/// <b>The single most useful field on an organization page for a newly arrived member</b>, and the
/// reason it is a row rather than a comma-separated column: "which churches here hold a service in
/// Twi" is a real question, and a CSV cannot be indexed to answer it.
/// </para>
/// <para>
/// The name is stored as the organization writes it rather than as a language code. A code would be
/// tidier and would lose the distinction between the varieties members actually name.
/// </para>
/// </summary>
public sealed class OrganizationLanguage
{
    private OrganizationLanguage(string name, int order)
    {
        Name = name;
        Order = order;
    }

    private OrganizationLanguage() { }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Most prominent first. A congregation lists its main language before its second.</summary>
    public int Order { get; private set; }

    public static OrganizationLanguage Create(string? name, int order = 0)
    {
        var trimmed = name?.Trim() ?? string.Empty;

        if (trimmed.Length > CommunityLifeLimits.MaxLanguageName)
        {
            trimmed = trimmed[..CommunityLifeLimits.MaxLanguageName];
        }

        return new OrganizationLanguage(trimmed, Math.Max(0, order));
    }
}
