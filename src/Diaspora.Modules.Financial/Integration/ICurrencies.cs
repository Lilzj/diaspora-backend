namespace Diaspora.Modules.Financial.Integration;

/// <summary>
/// What another module may ask B15 about a currency.
/// <para>
/// <b>Two questions, not five.</b> <c>CurrencyCatalog</c> also enumerates the catalogue and
/// invalidates its cache, and neither is another module's business: one is a financial screen's
/// read and the other is this module's own cache lifecycle. A seam that published everything the
/// service could do would make every one of those a compatibility promise.
/// </para>
/// <para>
/// <b>Asking is not the same as trusting the answer blindly.</b> <see cref="FactsAsync"/> never
/// fails: an unknown currency answers with its own code as the symbol and the platform's default
/// exponent, because refusing to render an amount over a catalogue miss makes a wallet unreadable
/// for a reason that has nothing to do with the wallet. A caller that needs to *refuse* an unknown
/// currency asks <see cref="IsSupportedAsync"/> first, which is what B18 does before it prices a
/// membership tier.
/// </para>
/// </summary>
public interface ICurrencies
{
    /// <summary>Whether the catalogue carries this code. The question to ask before refusing one.</summary>
    Task<bool> IsSupportedAsync(string currency, CancellationToken cancellationToken = default);

    /// <summary>
    /// The symbol and exponent for a code, always. See the note above on why this cannot fail.
    /// </summary>
    Task<CurrencyFacts> FactsAsync(string currency, CancellationToken cancellationToken = default);
}

/// <summary>
/// One currency as the rest of the platform needs it.
/// <para>
/// <see cref="Exponent"/> is the number of minor units — 2 for most currencies, 0 for JPY and KES
/// cents-free amounts, 3 for the dinars. A caller that assumes two will be wrong about real money.
/// </para>
/// </summary>
public sealed record CurrencyFacts(string Code, string Symbol, int Exponent);
