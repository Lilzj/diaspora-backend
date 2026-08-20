namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// One currency the platform knows how to denominate and format.
/// <para>
/// Served so a client has one formatter reading <see cref="Symbol"/> and <see cref="Exponent"/>,
/// instead of the two-currency table currently hard-coded in <c>JobCard.tsx</c>.
/// </para>
/// </summary>
public sealed record CurrencyResponse
{
    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string Symbol { get; init; }

    /// <summary>Minor digits. 2 for GBP, 0 for JPY, 3 for KWD.</summary>
    public required int Exponent { get; init; }

    public required bool IsActive { get; init; }
}
