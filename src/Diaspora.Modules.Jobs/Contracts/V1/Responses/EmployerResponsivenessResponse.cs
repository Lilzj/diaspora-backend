namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// How an employer actually treats applicants — measured, not claimed.
/// <para>
/// <b>Every rate is nullable, and null means "not enough evidence".</b> A rate computed from one
/// or two events is noise presented as a fact about a named organisation, and the platform
/// generated it. Below the sample threshold the field is null and a client renders nothing rather
/// than a number.
/// </para>
/// <para>
/// Publishing this at all is the strongest single incentive the platform can create for employers
/// to behave well towards people who applied across a border and cannot follow up in person.
/// </para>
/// </summary>
public sealed record EmployerResponsivenessResponse
{
    /// <summary>Mean hours to a first response. Null when nothing has been answered.</summary>
    public double? AverageResponseHours { get; init; }

    /// <summary>Share of applications never answered. Null below the sample threshold.</summary>
    public double? GhostingRate { get; init; }

    /// <summary>Share of offers accepted. Null below the sample threshold.</summary>
    public double? OfferAcceptanceRate { get; init; }

    /// <summary>Applications taken to a decision, either way.</summary>
    public required int DecidedCount { get; init; }
}
