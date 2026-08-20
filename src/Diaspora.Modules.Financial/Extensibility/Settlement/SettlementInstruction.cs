using Diaspora.Modules.Financial.Domain.ValueObjects;

namespace Diaspora.Modules.Financial.Extensibility.Settlement;

/// <summary>One counterparty, one amount, one payout run that has not been built.</summary>
public sealed record SettlementInstruction
{
    public required Guid CounterpartyId { get; init; }

    public required Money Amount { get; init; }

    public required DateTimeOffset PeriodStart { get; init; }

    public required DateTimeOffset PeriodEnd { get; init; }

    public string? Reference { get; init; }
}
