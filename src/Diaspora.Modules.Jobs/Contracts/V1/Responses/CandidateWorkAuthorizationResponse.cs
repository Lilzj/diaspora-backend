namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// A candidate's stated right to work in one country.
/// <para>
/// <b>Per country, which is the whole point.</b> A nurse with settled status in the UK, a permit
/// for Ireland and no right in Canada has three answers, and a single global flag makes her
/// explain it in a cover letter every time.
/// </para>
/// <para>
/// Everything here is the candidate's own statement. Nothing in this module verifies immigration
/// status — Trust owns verification, and the platform runs no background checks.
/// </para>
/// </summary>
public sealed record CandidateWorkAuthorizationResponse
{
    public required string CountryCode { get; init; }

    /// <summary><c>Citizen</c>, <c>PermanentResident</c>, <c>WorkPermit</c>, <c>RequiresSponsorship</c>, …</summary>
    public required string Kind { get; init; }

    public DateOnly? ExpiresOn { get; init; }

    /// <summary>Whether they can work there without the employer sponsoring.</summary>
    public required bool IsUnrestricted { get; init; }

    public required bool HasExpired { get; init; }
}
