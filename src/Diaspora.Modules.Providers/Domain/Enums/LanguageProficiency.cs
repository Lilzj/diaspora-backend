namespace Diaspora.Modules.Providers.Domain.Enums;

/// <summary>
/// How well a provider serves customers in a language.
/// <para>
/// A level rather than a flat list of language codes because "we speak Yoruba" spans a
/// receptionist who can greet you and a solicitor who can take instructions in it, and for a
/// member choosing an immigration adviser that difference is the whole decision. The client's
/// <c>mockLanguages</c> is a flat catalogue with no level; B7 keeps the catalogue and adds the
/// dimension the catalogue was missing.
/// </para>
/// </summary>
public enum LanguageProficiency
{
    /// <summary>Enough to greet and take basic details.</summary>
    Basic = 1,

    /// <summary>Comfortable for ordinary business.</summary>
    Conversational = 2,

    /// <summary>Fully fluent, including written work.</summary>
    Fluent = 3,

    /// <summary>A first language.</summary>
    Native = 4
}
