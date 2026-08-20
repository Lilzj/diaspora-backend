using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Domain.Providers;

/// <summary>
/// A professional credential a provider claims.
/// <para>
/// <b>Claimed is not proven, and the model must never blur the two.</b> A provider types "Gas
/// Safe registered" into a form and it lands here as <see cref="CertificationStatus.Claimed"/>
/// — useful to a member as something to ask about, and not evidence of anything. Only Trust can
/// move it to <see cref="CertificationStatus.Verified"/>, through
/// <see cref="MarkVerified"/>, which is internal for exactly that reason. A model that let the
/// provider set its own verified flag would put a self-awarded safety credential on a boiler
/// engineer's profile, which is a category of harm worth designing against explicitly.
/// </para>
/// <para>
/// <see cref="ExpiresOn"/> is checked at read time as well as swept, because a credential that
/// lapsed this morning must stop being shown as current this morning and not whenever a
/// background job next runs.
/// </para>
/// </summary>
public sealed class ProviderCertification : Entity<ProviderCertificationId>
{
    public const int MaxNameLength = 200;

    public const int MaxIssuerLength = 200;

    public const int MaxReferenceLength = 120;

    private ProviderCertification(
        ProviderCertificationId id, string name, string issuer, DateOnly? issuedOn, DateOnly? expiresOn)
        : base(id)
    {
        Name = name;
        Issuer = issuer;
        IssuedOn = issuedOn;
        ExpiresOn = expiresOn;
    }

    private ProviderCertification() { }

    /// <summary>"Gas Safe Register", "OISC Level 2", "Level 3 Food Hygiene".</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Who awarded it. Required — an unattributed credential is not a credential.</summary>
    public string Issuer { get; private set; } = string.Empty;

    /// <summary>The registration or licence number, where the provider publishes it.</summary>
    public string? Reference { get; private set; }

    public DateOnly? IssuedOn { get; private set; }

    /// <summary>Null when the credential does not lapse.</summary>
    public DateOnly? ExpiresOn { get; private set; }

    public CertificationStatus Status { get; private set; } = CertificationStatus.Claimed;

    /// <summary>The scan or photograph. A Media reference, never a URL.</summary>
    public MediaId? EvidenceMediaId { get; private set; }

    /// <summary>Trust's verification request, once one has been raised.</summary>
    public Guid? VerificationRequestId { get; private set; }

    public DateTimeOffset? VerifiedAt { get; private set; }

    public static Result<ProviderCertification> Create(
        ProviderCertificationId id,
        string? name,
        string? issuer,
        DateOnly? issuedOn = null,
        DateOnly? expiresOn = null,
        string? reference = null,
        MediaId? evidenceMediaId = null,
        DateOnly? today = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        var trimmedName = name?.Trim();

        if (string.IsNullOrEmpty(trimmedName) || trimmedName.Length > MaxNameLength)
        {
            return Result.Failure<ProviderCertification>(ProviderErrors.NameEmpty);
        }

        var trimmedIssuer = issuer?.Trim();

        if (string.IsNullOrEmpty(trimmedIssuer) || trimmedIssuer.Length > MaxIssuerLength)
        {
            return Result.Failure<ProviderCertification>(ProviderErrors.CertificationIssuerRequired);
        }

        if (today is { } asAt && expiresOn is { } expiry && expiry < asAt)
        {
            return Result.Failure<ProviderCertification>(ProviderErrors.CertificationExpired);
        }

        return Result.Success(new ProviderCertification(id, trimmedName, trimmedIssuer, issuedOn, expiresOn)
        {
            Reference = Trim(reference, MaxReferenceLength),
            EvidenceMediaId = evidenceMediaId
        });
    }

    public void AttachEvidence(MediaId? mediaId) => EvidenceMediaId = mediaId;

    /// <summary>Records that evidence has gone to Trust. The only status a provider can cause.</summary>
    public void MarkSubmitted(Guid verificationRequestId)
    {
        VerificationRequestId = verificationRequestId;
        Status = CertificationStatus.Submitted;
    }

    /// <summary>Internal: reachable only from a Trust integration event handler.</summary>
    internal void MarkVerified(DateTimeOffset verifiedAt)
    {
        Status = CertificationStatus.Verified;
        VerifiedAt = verifiedAt;
    }

    internal void MarkRejected() => Status = CertificationStatus.Rejected;

    internal void MarkRevoked() => Status = CertificationStatus.Revoked;

    internal void MarkExpired() => Status = CertificationStatus.Expired;

    /// <summary>
    /// Whether this credential may be presented as proven, as at a date. Expiry is evaluated
    /// here rather than trusted from <see cref="Status"/>, so a credential that lapsed overnight
    /// is not shown as current until a sweeper catches up.
    /// </summary>
    public bool IsCurrentlyVerified(DateOnly today) =>
        Status == CertificationStatus.Verified && (ExpiresOn is null || ExpiresOn >= today);

    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, max)];
}
