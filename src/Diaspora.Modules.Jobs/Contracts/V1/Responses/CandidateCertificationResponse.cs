namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// One professional certification.
/// <para>
/// <see cref="HasExpired"/> is computed rather than left to the client. A lapsed licence to
/// practise is worse than none — a candidate who does not notice applies for work they cannot
/// lawfully do — and every reader should get the same answer.
/// </para>
/// </summary>
public sealed record CandidateCertificationResponse
{
    public required Guid EntryId { get; init; }

    public required string Name { get; init; }

    public string? Issuer { get; init; }

    public string? CredentialId { get; init; }

    /// <summary>Where it was issued, which decides where it is valid.</summary>
    public string? CountryCode { get; init; }

    public DateOnly? IssuedOn { get; init; }

    public DateOnly? ExpiresOn { get; init; }

    public required bool HasExpired { get; init; }

    /// <summary>A Media reference to the uploaded certificate. Never a URL.</summary>
    public Guid? MediaId { get; init; }
}
