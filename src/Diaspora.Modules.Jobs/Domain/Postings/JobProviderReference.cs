using Diaspora.Modules.Jobs.Domain.Policies;

namespace Diaspora.Modules.Jobs.Domain.Postings;

/// <summary>
/// A provider a posting points at — the recruitment agency behind it, the immigration
/// consultant it recommends, the CV review service or career coach it offers.
/// <para>
/// <b>An identifier and editorial text, and nothing else.</b> The provider's name, badge,
/// rating and logo are B7's answers at read time, so an agency that renames itself or loses its
/// verification is never misrepresented by a posting written six months ago. Copying those
/// fields here would be duplicating ownership, which is the one thing this phase must not do.
/// </para>
/// </summary>
public sealed class JobProviderReference
{
    private JobProviderReference(Guid providerId, string? role, string? note, int order)
    {
        ProviderId = providerId;
        Role = role;
        Note = note;
        Order = order;
    }

    private JobProviderReference() { }

    /// <summary>B7's identifier. A value, not a foreign key.</summary>
    public Guid ProviderId { get; private set; }

    /// <summary>What this provider is to the posting — "recruiting agency", "visa support".</summary>
    public string? Role { get; private set; }

    /// <summary>The employer's own note about them.</summary>
    public string? Note { get; private set; }

    public int Order { get; private set; }

    internal static JobProviderReference Create(
        Guid providerId, string? role, string? note, int order)
    {
        var trimmedRole = Clip(role, JobLimits.MaxDisplayName);
        var trimmedNote = Clip(note, JobLimits.MaxBulletLength);

        return new JobProviderReference(providerId, trimmedRole, trimmedNote, order);
    }

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= max ? trimmed : trimmed[..max].TrimEnd();
    }
}
