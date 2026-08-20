using Diaspora.Modules.Jobs.Domain.Enums;

namespace Diaspora.Modules.Jobs.Domain.Candidates;

/// <summary>
/// A candidate's right to work in one country.
/// <para>
/// <b>The single most important thing on a diaspora candidate's profile, and the one most job
/// products model wrong.</b> They store one global "requires sponsorship" flag; the real answer
/// is per country, because a Nigerian nurse with settled status in the UK, a work permit for
/// Ireland and no right at all in Canada has three different answers, and a single flag makes
/// her explain it in a cover letter every single time.
/// </para>
/// <para>
/// <b>Every field here is the candidate's own statement.</b> Nothing in this module verifies
/// immigration status, and it must not: Trust owns verification, and the platform runs no
/// background checks — explicitly out of scope. What this enables is a candidate filtering to
/// jobs they can actually take, and an employer that sponsors finding the people who need it.
/// </para>
/// </summary>
public sealed class CandidateWorkAuthorization
{
    private CandidateWorkAuthorization(
        string countryCode, WorkAuthorizationKind kind, DateOnly? expiresOn, int order)
    {
        CountryCode = countryCode;
        Kind = kind;
        ExpiresOn = expiresOn;
        Order = order;
    }

    private CandidateWorkAuthorization() { }

    /// <summary>Upper-case ISO 3166-1 alpha-2. The country this answer is about.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    public WorkAuthorizationKind Kind { get; private set; }

    /// <summary>When the permission lapses. Null for citizenship and settled status.</summary>
    public DateOnly? ExpiresOn { get; private set; }

    public int Order { get; private set; }

    /// <summary>Whether the candidate can take work here without the employer sponsoring.</summary>
    public bool IsUnrestricted =>
        Kind is WorkAuthorizationKind.Citizen
            or WorkAuthorizationKind.PermanentResident
            or WorkAuthorizationKind.TreatyRight;

    /// <summary>Whether this permission has lapsed as of a given date.</summary>
    public bool HasExpired(DateOnly asOf) => ExpiresOn is { } expiry && expiry < asOf;

    /// <summary>Normalises a country code, or null when it is not a valid alpha-2.</summary>
    public static string? NormalizeCountry(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return null;
        }

        var candidate = countryCode.Trim().ToUpperInvariant();

        return candidate.Length == 2 && candidate.All(char.IsAsciiLetterUpper) ? candidate : null;
    }

    internal static CandidateWorkAuthorization? Create(
        string? countryCode, WorkAuthorizationKind kind, DateOnly? expiresOn, int order)
    {
        var country = NormalizeCountry(countryCode);

        if (country is null || !Enum.IsDefined(kind))
        {
            return null;
        }

        // Citizenship and settled status do not expire. A date on one is a client sending the
        // whole form back with a stale field, and keeping it would eventually hide the
        // candidate from jobs they are perfectly entitled to take.
        var expiry = kind is WorkAuthorizationKind.Citizen
            or WorkAuthorizationKind.PermanentResident
            ? null
            : expiresOn;

        return new CandidateWorkAuthorization(country, kind, expiry, order);
    }
}
