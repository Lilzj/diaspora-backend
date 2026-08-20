namespace Diaspora.Modules.Trust.Contracts.V1.Requests;

/// <summary>
/// Submits the diaspora eligibility verification — the platform's core check.
/// <para>
/// Field names are the frontend's <c>SubmitVerificationRequest</c>
/// (<c>types/verification.ts:72</c>) verbatim, with one addition:
/// <see cref="DocumentNumber"/>. The wizard already collects it
/// (<c>diaspora.tsx:40</c>), validates it (<c>:169</c>) and displays it on the confirm step
/// (<c>:565</c>) — and then drops it, because the DTO has no field for it. A reviewer cannot
/// check a document against a registry without the number, so the backend accepts it.
/// </para>
/// <para>
/// <c>documentUri</c> is renamed <see cref="DocumentReference"/>: it has never held a URI —
/// the wizard puts a local filename there — and the value the server wants is a storage key.
/// </para>
/// </summary>
public sealed record SubmitDiasporaVerificationRequest
{
    /// <summary>ISO-3166-1 alpha-2 country code.</summary>
    public required string Nationality { get; init; }

    /// <summary>ISO-3166-1 alpha-2 country code. Must differ from <see cref="Nationality"/>.</summary>
    public required string ResidenceCountry { get; init; }

    /// <summary>One of the <c>DiasporaDocumentType</c> values the wizard offers.</summary>
    public required string DocumentType { get; init; }

    public required string IssueCountry { get; init; }

    /// <summary>ISO date, <c>YYYY-MM-DD</c>.</summary>
    public DateOnly? IssueDate { get; init; }

    /// <summary>ISO date, <c>YYYY-MM-DD</c>.</summary>
    public DateOnly? ExpiryDate { get; init; }

    /// <summary>The document's own number. New — see the type remarks.</summary>
    public string? DocumentNumber { get; init; }

    public string? SupportingNotes { get; init; }

    /// <summary>Storage key of the uploaded document.</summary>
    public required string DocumentReference { get; init; }

    /// <summary>Storage key of the biometric selfie. Required for this type.</summary>
    public string? SelfieReference { get; init; }
}

/// <summary>
/// Submits a country residency verification.
/// <para>
/// Adopts the frontend's <c>VerificationSubmitRequest</c> (<c>types/verification.ts:45</c>)
/// shape. Renamed from that anagram-of-the-other-one because the two frontend names —
/// <c>SubmitVerificationRequest</c> and <c>VerificationSubmitRequest</c> — differ only by
/// word order while describing entirely different flows, and neither is wired to HTTP yet,
/// so the rename costs nothing.
/// </para>
/// </summary>
public sealed record SubmitCountryVerificationRequest
{
    public required string CountryCode { get; init; }

    /// <summary>One of the <c>VerificationDocumentType</c> values.</summary>
    public required string DocumentType { get; init; }

    public required string DocumentReference { get; init; }

    /// <summary>ISO date. Drives the residence-duration requirement.</summary>
    public DateOnly? ArrivalDate { get; init; }

    /// <summary>Country-knowledge areas claimed. At most ten.</summary>
    public IReadOnlyList<string>? ExpertiseAreas { get; init; }

    public string? SupportingNotes { get; init; }
}

/// <summary>
/// Submits the government-ID and biometric verification.
/// <para>
/// The frontend posts <c>{ docType, country }</c> to <c>/trust/verify/identity</c>
/// (<c>identity.tsx:37</c>) — no document reference at all, because the mock never had to
/// store one. The backend requires the evidence it is being asked to judge.
/// </para>
/// </summary>
public sealed record SubmitIdentityVerificationRequest
{
    /// <summary>Passport, NationalId, DriverLicense, WorkPermit, ResidencePermit, StudentPermit.</summary>
    public required string DocumentType { get; init; }

    /// <summary>Where the document was issued.</summary>
    public required string IssueCountry { get; init; }

    public required string DocumentReference { get; init; }

    /// <summary>Required — this type is a face match.</summary>
    public required string SelfieReference { get; init; }

    public string? DocumentNumber { get; init; }

    public DateOnly? ExpiryDate { get; init; }
}

/// <summary>
/// Submits any other verification type through one generic route, so a type added by a
/// future <c>IVerificationTypeHandler</c> needs no new endpoint.
/// </summary>
public sealed record SubmitVerificationRequest
{
    /// <summary>Any supported <c>VerificationType</c>.</summary>
    public required string Type { get; init; }

    public string? Nationality { get; init; }

    public string? ResidenceCountry { get; init; }

    public string? TargetCountry { get; init; }

    public DateOnly? ArrivalDate { get; init; }

    public string? SupportingNotes { get; init; }

    public string? SelfieReference { get; init; }

    public IReadOnlyList<string>? ExpertiseAreas { get; init; }

    public IReadOnlyList<AttachDocumentRequest>? Documents { get; init; }
}

/// <summary>Attaches one document to a verification request.</summary>
public sealed record AttachDocumentRequest
{
    public required string DocumentType { get; init; }

    /// <summary>Storage key returned by the upload.</summary>
    public required string DocumentReference { get; init; }

    public string? DocumentNumber { get; init; }

    public string? IssuingCountry { get; init; }

    public DateOnly? IssueDate { get; init; }

    public DateOnly? ExpiryDate { get; init; }

    public string? FileName { get; init; }

    public string? ContentType { get; init; }

    public long SizeInBytes { get; init; }
}

/// <summary>
/// A reviewer's decision on a verification. Staff only.
/// <para>
/// One request for every outcome rather than five endpoints, because a reviewer's action is
/// one decision with a shape that varies — and five near-identical routes would drift.
/// </para>
/// </summary>
public sealed record ReviewVerificationRequest
{
    /// <summary>Approve, PartiallyApprove, Reject, NeedMoreInfo, Escalate, Revoke.</summary>
    public required string Decision { get; init; }

    /// <summary>Required when rejecting. One of the <c>VerificationRejectionReason</c> values.</summary>
    public string? RejectionReason { get; init; }

    /// <summary>Shown to the member. Required when rejecting or asking for more.</summary>
    public string? Details { get; init; }

    /// <summary>Internal reviewer notes.</summary>
    public string? Notes { get; init; }
}
