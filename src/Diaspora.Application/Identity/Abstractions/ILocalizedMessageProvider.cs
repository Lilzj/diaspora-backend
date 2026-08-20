namespace Diaspora.Application.Identity.Abstractions;

/// <summary>
/// Resolves a message key to text in the caller's language.
/// <para>
/// Validators emit keys rather than literals so a Portuguese-speaking member in Lisbon
/// and an English-speaking member in Toronto get the same rule explained in their own
/// language, without duplicating the rule.
/// </para>
/// </summary>
public interface ILocalizedMessageProvider
{
    /// <summary>
    /// Returns the message for <paramref name="key"/> in the ambient language, falling
    /// back to the base language and then to the default. Returns the key itself if
    /// nothing matches, which makes a missing translation obvious rather than silent.
    /// </summary>
    string GetMessage(string key);

    string GetMessage(string key, params object[] arguments);

    string GetFor(string key, string? language);
}
