namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// What a posting pays.
/// <para>
/// <see cref="IsDisclosed"/> is false on most postings and that is a real answer, not a missing
/// one — it lets a client render "salary not disclosed" rather than nothing, which is a
/// meaningful difference to a candidate deciding whether to spend an hour applying.
/// </para>
/// <para>
/// <b>No converted figure is ever present.</b> The currency is the employer's and the number is
/// theirs. A converted amount would need a rate, a rate has a date, and a salary silently
/// restated at yesterday's rate is a number nobody can act on.
/// </para>
/// </summary>
public sealed record JobSalaryResponse
{
    public required bool IsDisclosed { get; init; }

    public decimal? Minimum { get; init; }

    /// <summary>Null when the employer quoted a single figure rather than a band.</summary>
    public decimal? Maximum { get; init; }

    /// <summary>ISO 4217, upper case.</summary>
    public string? CurrencyCode { get; init; }

    /// <summary><c>Yearly</c>, <c>Monthly</c>, <c>Hourly</c>, …</summary>
    public string? Period { get; init; }

    public required bool IsNegotiable { get; init; }

    /// <summary>Whether the posting states the work is unpaid. Volunteer roles.</summary>
    public required bool IsUnpaid { get; init; }
}
