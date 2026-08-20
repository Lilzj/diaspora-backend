namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// How well a candidate speaks a language, on the scale employers actually use.
/// <para>
/// Deliberately not CEFR. A1–C2 is more precise and almost nobody self-reports it accurately;
/// these five are the rungs that appear on real job adverts, so a candidate's answer and an
/// employer's requirement are denominated in the same units.
/// </para>
/// </summary>
public enum LanguageProficiency
{
    /// <summary>Enough to get by.</summary>
    Basic = 1,

    Conversational = 2,

    /// <summary>Can work in it.</summary>
    Professional = 3,

    /// <summary>Complete command, short of a first language.</summary>
    Fluent = 4,

    Native = 5
}
