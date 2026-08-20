namespace Diaspora.Modules.Jobs.Domain.Candidates;

/// <summary>One certification as the profile editor sends it.</summary>
public sealed record CandidateCertificationDraft
{
    public Guid EntryId { get; init; }

    public required string? Name { get; init; }

    public string? Issuer { get; init; }

    public string? CredentialId { get; init; }

    public string? CountryCode { get; init; }

    public DateOnly? IssuedOn { get; init; }

    public DateOnly? ExpiresOn { get; init; }

    /// <summary>A B5 reference to the uploaded certificate. Never a URL.</summary>
    public Guid? MediaId { get; init; }
}
