using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Domain.Policies;

namespace Diaspora.Modules.Guides.Domain.Emergency;

/// <summary>
/// One number somebody in trouble should call.
/// <para>
/// <b>The kind is an open key rather than an enum, and that is the brief's requirement rather than a
/// shortcut.</b> "Future categories configurable" is not decoration here: the ten the brief names —
/// police, fire, ambulance, disaster management, embassy line, medical, women's safety, child
/// protection, domestic violence, mental health — are the ten that generalise, and every country adds
/// its own. Mauritius has a cyclone line. Nigeria has a road-safety corps. A closed enum would mean a
/// deployment either files those under "other" or waits for a release.
/// </para>
/// <para>
/// <b>The label is written per country, not derived from the kind.</b> The body a Francophone country
/// calls when it needs police is the <em>gendarmerie</em>, and rendering the key's English name over
/// its number would be both wrong and, in a directory somebody reads while frightened, unhelpful in
/// the worst possible way.
/// </para>
/// </summary>
public sealed class EmergencyContact
{
    private EmergencyContact(
        string kind,
        string label,
        string number,
        string? alternateNumber,
        string? note,
        bool isAvailableAllHours,
        int position)
    {
        Kind = kind;
        Label = label;
        Number = number;
        AlternateNumber = alternateNumber;
        Note = note;
        IsAvailableAllHours = isAvailableAllHours;
        Position = position;
    }

    private EmergencyContact() { }

    /// <summary>A stable lower-case key — <c>police</c>, <c>mental-health</c>. The row's identity.</summary>
    public string Kind { get; private set; } = string.Empty;

    /// <summary>What this country calls the service. Written by an editor, not derived.</summary>
    public string Label { get; private set; } = string.Empty;

    /// <summary>The number to dial, kept as written. Frequently short and not internationally dialable.</summary>
    public string Number { get; private set; } = string.Empty;

    /// <summary>A second number, when the service publishes one. Rendered beside the first.</summary>
    public string? AlternateNumber { get; private set; }

    /// <summary>Hours, languages, coverage — whatever the caller needs to know before dialling.</summary>
    public string? Note { get; private set; }

    /// <summary>
    /// Whether it answers at any hour. Rendered as a badge, because it is the one thing somebody
    /// checks at two in the morning and the one thing a paragraph buries.
    /// </summary>
    public bool IsAvailableAllHours { get; private set; }

    /// <summary>Display order. Contiguous from zero — the editor decides what leads.</summary>
    public int Position { get; private set; }

    internal static EmergencyContact? TryCreate(
        string? kind,
        string? label,
        string? number,
        string? alternateNumber,
        string? note,
        bool isAvailableAllHours,
        int position)
    {
        var normalizedKind = IntelligenceNormalization.Key(kind);
        var trimmedLabel = IntelligenceNormalization.Text(
            label, IntelligenceLimits.MaxEmergencyLabel);
        var normalizedNumber = IntelligenceNormalization.Phone(number);

        return normalizedKind is null || trimmedLabel is null || normalizedNumber is null
            ? null
            : new EmergencyContact(
                normalizedKind,
                trimmedLabel,
                normalizedNumber,
                IntelligenceNormalization.Phone(alternateNumber),
                IntelligenceNormalization.Text(note, IntelligenceLimits.MaxEmergencyNote),
                isAvailableAllHours,
                position);
    }
}
