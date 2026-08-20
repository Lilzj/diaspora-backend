namespace Diaspora.Modules.Financial.Extensibility.Catalog;

/// <summary>One currency a module wants the platform to know about at startup.</summary>
public sealed record CurrencySeed
{
    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string Symbol { get; init; }

    /// <summary>Minor digits. 2 for GBP, 0 for JPY, 3 for KWD.</summary>
    public int Exponent { get; init; } = 2;

    public int SortOrder { get; init; } = 100;
}
