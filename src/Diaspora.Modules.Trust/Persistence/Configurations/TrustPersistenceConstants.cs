namespace Diaspora.Modules.Trust.Persistence.Configurations;

/// <summary>Values shared by the module's entity configurations.</summary>
public static class TrustPersistenceConstants
{
    /// <summary>
    /// Delimiter for the short string lists stored inline rather than in a join table —
    /// expertise areas, AI flags, appeal evidence keys.
    /// <para>
    /// ASCII unit separator (U+001F), not a comma. Every one of those lists holds free text
    /// or member-supplied keys, and any printable delimiter is a character somebody
    /// eventually types. A control character cannot survive the input validation these
    /// fields already run, so it cannot appear in a stored value and split one row into two.
    /// </para>
    /// </summary>
    public const char ListSeparator = '\u001F';
}
