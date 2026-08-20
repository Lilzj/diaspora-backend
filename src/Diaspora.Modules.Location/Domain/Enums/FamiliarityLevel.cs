namespace Diaspora.Modules.Location.Domain.Enums;

/// <summary>
/// A member's self-assessed fluency with a country's language or culture.
/// <para>
/// Self-assessed and explicitly so: unlike <see cref="ExperienceLevel"/> there is no fact
/// the platform could derive this from, and pretending otherwise would be worse than asking.
/// It informs matching and ranking; it never gates anything.
/// </para>
/// </summary>
public enum FamiliarityLevel
{
    None = 0,
    Basic = 1,
    Conversational = 2,
    Fluent = 3,
    Native = 4
}
