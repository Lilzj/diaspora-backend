namespace Diaspora.Modules.Financial.Extensibility.Currency;

/// <summary>
/// One rate, at one moment.
/// <para>
/// <see cref="AsOf"/> is required rather than optional, and that is the whole point of the type. A
/// rate without a date is a number somebody will treat as current for ever; the platform's rule is
/// that a price silently restated at yesterday's rate is a number a buyer would act on and be wrong
/// about.
/// </para>
/// </summary>
public sealed record ExchangeRate
{
    public required string FromCurrency { get; init; }

    public required string ToCurrency { get; init; }

    /// <summary>Multiply an amount in <see cref="FromCurrency"/> by this.</summary>
    public required decimal Rate { get; init; }

    public required DateTimeOffset AsOf { get; init; }

    /// <summary>Where the rate came from. Recorded so a converted figure can be defended.</summary>
    public string? Source { get; init; }
}
