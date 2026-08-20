using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Domain.Policies;

namespace Diaspora.Modules.Guides.Domain.Embassies;

/// <summary>
/// One thing a mission does — passport renewal, document legalisation, emergency travel documents.
/// <para>
/// <b>A keyed row rather than a boolean per service, and the difference is what a deployment can
/// add.</b> The four headline capabilities the brief names — passports, visas, consular, emergency
/// assistance — are flags on the mission itself, because every client filters on them and a filter
/// over a child collection is a join every consumer pays for. This collection is everything else,
/// and it is open-ended: birth registration, police clearance, pension attestation, notarial acts.
/// A deployment adds one by writing a row.
/// </para>
/// <para>
/// <see cref="RequiresAppointment"/> sits per service rather than per mission because that is how it
/// is true: a mission that takes walk-ins for document collection and appointments for visa
/// interviews is the normal case, and one flag on the mission has to be wrong for one of them.
/// </para>
/// </summary>
public sealed class EmbassyService
{
    private EmbassyService(
        string key, string name, string? description, bool requiresAppointment, int position)
    {
        Key = key;
        Name = name;
        Description = description;
        RequiresAppointment = requiresAppointment;
        Position = position;
    }

    private EmbassyService() { }

    /// <summary>A stable lower-case key. The row's identity within its mission.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>What the mission calls it. Localized through the mission's translations.</summary>
    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>Whether somebody must book before turning up. Per service, because it varies.</summary>
    public bool RequiresAppointment { get; private set; }

    /// <summary>Display order. Contiguous from zero.</summary>
    public int Position { get; private set; }

    internal static EmbassyService? TryCreate(
        string? key, string? name, string? description, bool requiresAppointment, int position)
    {
        var normalizedKey = IntelligenceNormalization.Key(key);
        var trimmedName = IntelligenceNormalization.Text(name, IntelligenceLimits.MaxServiceName);

        return normalizedKey is null || trimmedName is null
            ? null
            : new EmbassyService(
                normalizedKey,
                trimmedName,
                IntelligenceNormalization.Text(
                    description, IntelligenceLimits.MaxServiceDescription),
                requiresAppointment,
                position);
    }
}
