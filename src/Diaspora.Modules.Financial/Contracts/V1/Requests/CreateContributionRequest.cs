namespace Diaspora.Modules.Financial.Contracts.V1.Requests;

/// <summary>
/// Contribute to a community fund — money, goods, time or skills.
/// <para>
/// <b>Four of the five kinds have no amount, and the request has to be able to say so.</b> A
/// monetary contribution needs <see cref="Amount"/> and <see cref="Currency"/>; the other four need
/// <see cref="PledgeQuantity"/> and <see cref="PledgeUnit"/>. Sending the wrong pair is refused with
/// a specific code rather than silently stored as a zero.
/// </para>
/// </summary>
public sealed record CreateContributionRequest
{
    public required string FundCode { get; init; }

    /// <summary>
    /// <c>Monetary</c>, <c>Goods</c>, <c>VolunteerService</c>, <c>ProfessionalSkills</c> or
    /// <c>Supplies</c>.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>Major units. Required for <c>Monetary</c>, ignored otherwise.</summary>
    public decimal? Amount { get; init; }

    public string? Currency { get; init; }

    /// <summary>How much of what. Required for the four non-monetary kinds.</summary>
    public decimal? PledgeQuantity { get; init; }

    /// <summary><c>items</c>, <c>hours</c>, <c>boxes</c>. Whatever the organiser counts in.</summary>
    public string? PledgeUnit { get; init; }

    public string? PledgeDescription { get; init; }

    public bool IsAnonymous { get; init; }

    public string? Message { get; init; }

    public string? MethodKind { get; init; }

    public string? CountryCode { get; init; }

    public string? IdempotencyKey { get; init; }
}
