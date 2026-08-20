namespace Diaspora.Modules.Guides.Extensibility.Intelligence;

/// <summary>
/// How a deployment adds a kind of emergency contact.
/// <para>
/// The brief asks for exactly this — "future categories configurable" — and the answer here is a
/// vocabulary rather than a catalogue table, because a kind carries no editorial content of its own.
/// See <see cref="EmergencyContactKindSeed"/> for why.
/// </para>
/// <para>
/// <b>Contributed kinds are additive and never restrictive.</b> A country that stores a contact under
/// a kind nobody declared is still served, still rendered and still dialable; the vocabulary decides
/// what a picker offers and what a client can find an icon for, not what the database will accept.
/// Making it a whitelist would mean a deployment's own emergency number stops appearing the day
/// somebody reorders the contributor registrations.
/// </para>
/// </summary>
public interface IEmergencyContactKindContributor
{
    /// <summary>The module contributing. Appears in diagnostics.</summary>
    string Module { get; }

    /// <summary>Lower runs first, and first wins on a repeated key.</summary>
    int Order => 100;

    IEnumerable<EmergencyContactKindSeed> GetKinds();
}
