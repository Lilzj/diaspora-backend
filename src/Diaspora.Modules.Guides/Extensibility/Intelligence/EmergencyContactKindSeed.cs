namespace Diaspora.Modules.Guides.Extensibility.Intelligence;

/// <summary>
/// One kind of emergency contact a deployment recognises — <c>police</c>, <c>mental-health</c>,
/// <c>cyclone-warning</c>.
/// <para>
/// <b>A vocabulary rather than a table, and it is the only seam in this phase with no rows behind
/// it.</b> The kind lives on the contact itself, where it belongs: what a country calls its police
/// and what number they answer on are one editorial decision, made per country. What this declares is
/// which kinds the platform <em>knows about</em> — which is what a client's icon map switches on,
/// what an editing screen offers in a picker, and what the capabilities endpoint returns.
/// </para>
/// <para>
/// The label here is the fallback for a deployment's own screens, never what a reader sees. A reader
/// sees the label the country's editor wrote, because the body a Francophone country calls for police
/// is the gendarmerie and rendering an English key over its number helps nobody.
/// </para>
/// </summary>
public sealed record EmergencyContactKindSeed
{
    /// <summary>Lower-case kebab. What an <c>EmergencyContact</c> stores and a client switches on.</summary>
    public required string Key { get; init; }

    /// <summary>The platform's own name for it. Editorial screens only.</summary>
    public required string Name { get; init; }

    /// <summary>An emoji or icon name. Opaque to the server.</summary>
    public string? Icon { get; init; }

    /// <summary>Suggested position in an editing screen's picker. Lower first.</summary>
    public int Order { get; init; }

    /// <summary>
    /// Whether a country directory without this kind is worth flagging to an editor.
    /// <para>
    /// Police, fire and ambulance are essential nearly everywhere; a domestic-violence hotline is
    /// essential and does not exist everywhere. The flag drives a completeness hint on the editorial
    /// screen and nothing else — it never blocks a save, because a country that genuinely has no
    /// separate fire number must still be publishable.
    /// </para>
    /// </summary>
    public bool IsEssential { get; init; }
}
