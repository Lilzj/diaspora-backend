namespace Diaspora.Modules.Financial.Extensibility.Currency;

/// <summary>
/// The currency to speak to a member in when nothing more specific says.
/// <para>
/// <b>The rule this enforces.</b> A member's currency comes from their country of residence, never
/// from a hard-coded GBP. Before 14 Oct 2026 six Financial handlers, the creator revenue summary and
/// the advertising dashboard each answered "no currency was named" with the platform default, so a
/// member resident in Mauritius was quoted limits, terms and empty totals in sterling.
/// </para>
/// <para>
/// <b>Order of precedence.</b> Money the member already holds wins: their personal account's
/// currency, because restating existing money in a different currency would be a conversion and
/// the platform never converts. Then their country of residence. Then the configured default, for a
/// member whose country is unknown or a market the platform has no currency for.
/// </para>
/// </summary>
public interface IMemberCurrency
{
    /// <summary>An ISO 4217 code. Never null; the configured default is the last resort.</summary>
    Task<string> ForMemberAsync(Guid userId, CancellationToken cancellationToken = default);
}
