namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// What a candidate is looking to earn.
/// <para>
/// <b>Only ever sent to the candidate themselves.</b> It is a filter input on their own searches
/// and nothing else reads it.
/// </para>
/// </summary>
public sealed record CandidateSalaryExpectationResponse
{
    public decimal? Minimum { get; init; }

    public string? CurrencyCode { get; init; }

    public string? Period { get; init; }

    public required bool IsFlexible { get; init; }
}
