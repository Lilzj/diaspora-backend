namespace Diaspora.Modules.Financial.Extensibility.Search;

/// <summary>
/// A financial record as something searchable.
/// <para>
/// <b>Flat, primitive and provider-independent.</b> No engine is named, no index is created and no
/// query language appears anywhere in this module — the same position every other vertical takes,
/// and the reason the platform has nine such seams and nine database-query defaults behind them.
/// </para>
/// <para>
/// <b>An amount travels as minor units and a currency, never as a formatted string.</b> A search
/// index holding "£25.00" is an index nobody can range-query.
/// </para>
/// </summary>
public sealed record FinancialSearchDocument
{
    public required string DocumentId { get; init; }

    /// <summary><c>transaction</c>, <c>purchase</c>, <c>donation</c>, <c>contribution</c>.</summary>
    public required string DocumentType { get; init; }

    public required string Title { get; init; }

    public string? Summary { get; init; }

    public Guid? OwnerUserId { get; init; }

    public long? AmountMinorUnits { get; init; }

    public string? Currency { get; init; }

    public string? CountryCode { get; init; }

    public string? Status { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}
