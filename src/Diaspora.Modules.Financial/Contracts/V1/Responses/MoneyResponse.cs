namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// An amount, as every client on this platform already reads one.
/// <para>
/// <b><see cref="Amount"/> first and <see cref="MinorUnits"/> second, deliberately.</b> Every priced
/// object in the React Native client is <c>{ price: number, currency: string }</c> — a marketplace
/// listing, a housing rent, a salary, an event ticket — so a client written against that reads
/// <see cref="Amount"/> and works. A client that needs exactness reads <see cref="MinorUnits"/> and
/// never sees a rounding error, which is what the platform actually stores.
/// </para>
/// <para>
/// <b><see cref="Exponent"/> and <see cref="Symbol"/> exist because of one line in the client.</b>
/// <c>JobCard.tsx</c> maps GBP to a pound sign, MUR to "Rs " and everything else to the bare code —
/// a per-component table that will be copied into the next card and disagree with the first. Serving
/// both from the currency catalogue costs two fields and deletes the table, and the exponent is what
/// stops a client assuming two decimals and being wrong by a factor of a hundred about a quarter of
/// the world's currencies.
/// </para>
/// <para>
/// <b>There is no converted amount here and there never will be.</b> Every figure is in the currency
/// it was denominated in.
/// </para>
/// </summary>
public sealed record MoneyResponse
{
    /// <summary>The figure a card renders. Derived from <see cref="MinorUnits"/>.</summary>
    public required decimal Amount { get; init; }

    /// <summary>ISO 4217, upper case. Never converted.</summary>
    public required string Currency { get; init; }

    /// <summary>The exact stored value: a count of the currency's smallest unit.</summary>
    public required long MinorUnits { get; init; }

    /// <summary>Minor digits. 2 for GBP, 0 for JPY, 3 for KWD.</summary>
    public required int Exponent { get; init; }

    /// <summary>What to render in front of the amount. Null when the catalogue has no symbol.</summary>
    public string? Symbol { get; init; }
}
