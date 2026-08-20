using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Guides.Extensibility.Intelligence;

/// <summary>
/// A claim that a body is what it says it is, offered for verification.
/// <para>
/// <b>The subject is an institution and the submitter is a person, and both travel.</b> Trust reviews
/// people — it is a reputation system for members — so somebody has to be accountable for the claim
/// even though the thing being verified is a ministry. That separation is exactly what Trust's subject
/// model expresses, and B8 established the pattern for guides.
/// </para>
/// <para>
/// Flat primitives and shared-kernel identifiers only, so a provider can live in an assembly that has
/// never referenced this module's domain.
/// </para>
/// </summary>
public sealed record AuthorityVerificationRequest
{
    /// <summary>Which kind of subject this is — an issuing authority or a mission.</summary>
    public required AuthoritySubjectKind SubjectKind { get; init; }

    /// <summary>The authority's or mission's identifier, as a primitive.</summary>
    public required Guid SubjectId { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    public required string Name { get; init; }

    /// <summary>What kind of official body this claims to be.</summary>
    public required AuthorityType AuthorityType { get; init; }

    /// <summary>The body's own site, when one is recorded. A reviewer starts here.</summary>
    public string? Website { get; init; }

    /// <summary>The member accountable for the claim.</summary>
    public required UserId SubmittedBy { get; init; }

    /// <summary>Media references supporting the claim. References, never URLs.</summary>
    public IReadOnlyList<AuthorityVerificationEvidence> Evidence { get; init; } = [];

    /// <summary>Anything a reviewer should know that no field holds.</summary>
    public string? Notes { get; init; }
}

/// <summary>Which register the subject of a verification lives in.</summary>
public enum AuthoritySubjectKind
{
    /// <summary>An issuing authority — a ministry, a department, a police service.</summary>
    IssuingAuthority = 0,

    /// <summary>A mission in the embassy directory.</summary>
    Embassy = 1
}

/// <summary>
/// One piece of proof attached to an authority verification.
/// <para>
/// <b>The document type is required in spirit, and B3.0.2 is why.</b> Trust's type handlers refuse
/// untyped evidence before a reviewer ever sees it, so a submission that omits it fails silently at
/// the far end of an integration. The shipped provider stamps a default rather than passing null.
/// </para>
/// </summary>
public sealed record AuthorityVerificationEvidence
{
    /// <summary>A B5 media reference. Never a URL.</summary>
    public required Guid MediaId { get; init; }

    /// <summary>A Trust document-type name. Left null, the provider stamps its default.</summary>
    public string? DocumentType { get; init; }
}
