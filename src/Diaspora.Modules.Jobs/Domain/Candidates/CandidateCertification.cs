using Diaspora.Modules.Jobs.Domain.Policies;

namespace Diaspora.Modules.Jobs.Domain.Candidates;

/// <summary>
/// One professional certification a candidate holds.
/// <para>
/// <b><see cref="ExpiresOn"/> earns its place.</b> Licences to practise expire, and an expired
/// one is worse than none — a candidate who does not notice applies for work they cannot
/// lawfully do, and an employer who does not notice hires them. Recording the date is what lets
/// the platform tell both.
/// </para>
/// <para>
/// <see cref="MediaId"/> points at an uploaded certificate through B5, by reference. Nothing
/// here is verified: Trust owns verification, and a certificate on a profile is a claim with a
/// document attached, not proof.
/// </para>
/// </summary>
public sealed class CandidateCertification
{
    private CandidateCertification(
        Guid entryId,
        string name,
        string? issuer,
        string? credentialId,
        string? countryCode,
        DateOnly? issuedOn,
        DateOnly? expiresOn,
        Guid? mediaId,
        int order)
    {
        EntryId = entryId;
        Name = name;
        Issuer = issuer;
        CredentialId = credentialId;
        CountryCode = countryCode;
        IssuedOn = issuedOn;
        ExpiresOn = expiresOn;
        MediaId = mediaId;
        Order = order;
    }

    private CandidateCertification() { }

    public Guid EntryId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Issuer { get; private set; }

    /// <summary>The registration or licence number, where there is one.</summary>
    public string? CredentialId { get; private set; }

    /// <summary>Where it was issued. Decides where it is valid.</summary>
    public string? CountryCode { get; private set; }

    public DateOnly? IssuedOn { get; private set; }

    /// <summary>When it lapses. Null means it does not.</summary>
    public DateOnly? ExpiresOn { get; private set; }

    /// <summary>A B5 reference to the uploaded certificate. Never a URL.</summary>
    public Guid? MediaId { get; private set; }

    public int Order { get; private set; }

    /// <summary>Whether the certification has lapsed as of a given date.</summary>
    public bool HasExpired(DateOnly asOf) => ExpiresOn is { } expiry && expiry < asOf;

    internal static CandidateCertification? Create(
        Guid entryId,
        string? name,
        string? issuer,
        string? credentialId,
        string? countryCode,
        DateOnly? issuedOn,
        DateOnly? expiresOn,
        Guid? mediaId,
        int order)
    {
        var title = Clip(name, JobLimits.MaxDisplayName);

        if (title is null)
        {
            return null;
        }

        // An expiry before issue is a transposition. Dropping the pair is kinder than losing
        // the certification, and the candidate can correct it.
        if (issuedOn is { } issued && expiresOn is { } expires && expires < issued)
        {
            issuedOn = null;
            expiresOn = null;
        }

        return new CandidateCertification(
            entryId == Guid.Empty ? Guid.NewGuid() : entryId,
            title,
            Clip(issuer, JobLimits.MaxOrganizationName),
            Clip(credentialId, JobLimits.MaxRegistrationNumber),
            CandidateWorkAuthorization.NormalizeCountry(countryCode),
            issuedOn,
            expiresOn,
            mediaId == Guid.Empty ? null : mediaId,
            order);
    }

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= max ? trimmed : trimmed[..max].TrimEnd();
    }
}
