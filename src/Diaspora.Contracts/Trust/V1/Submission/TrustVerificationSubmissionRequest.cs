namespace Diaspora.Contracts.Trust.V1.Submission;

/// <summary>
/// A request to start a verification workflow.
/// <para>
/// <b>Submission is not approval.</b> Everything on this record is a claim, not a fact. Nothing
/// here can grant a badge, move a trust tier or set a verification status — Trust decides all
/// three, and this type exists only to open the case.
/// </para>
/// <para>
/// Flat primitives and published names throughout: no Trust entity, no Trust enum, no Media
/// entity. A Marketplace module must be able to build one of these with a project reference to
/// this assembly and nothing else, which is exactly the property that was missing before B3.0.2
/// and the reason B7 shipped a stub.
/// </para>
/// </summary>
public sealed record TrustVerificationSubmissionRequest
{
    /// <summary>
    /// Trust's <c>VerificationType</c> name — <c>Identity</c>, <c>Business</c>,
    /// <c>Organization</c>, <c>Professional</c>, <c>Embassy</c>, <c>Residence</c>, <c>Country</c>.
    /// <para>
    /// A string for the same reason Trust's published events carry it as one: a consumer must be
    /// able to name a type without referencing the module that owns the enum. An unknown name is
    /// refused with <c>trust.verification.type-unsupported</c> rather than guessed at.
    /// </para>
    /// </summary>
    public required string VerificationType { get; init; }

    /// <summary>
    /// The member the verification is about and whose standing it will affect.
    /// <para>
    /// Trust reviews people, not rows — even when the subject is a business, a human is
    /// accountable for the claim, and this is who that is.
    /// </para>
    /// </summary>
    public required Guid SubmittedBy { get; init; }

    /// <summary>
    /// What is under review. Defaults to the submitting member, which is what every submission
    /// meant before subjects existed.
    /// </summary>
    public TrustVerificationSubject Subject { get; init; } = TrustVerificationSubject.Member;

    /// <summary>
    /// Evidence, as Media references.
    /// <para>
    /// <b>References, never URLs, and never bytes.</b> Media owns the asset, its storage, its
    /// lifecycle and its access control; Trust records which assets were offered as evidence.
    /// This is B5's rule (ADR-005) and it is why a verification cannot leak a document to
    /// someone who may not read it — the link is minted by Media at read time, against the
    /// reader.
    /// </para>
    /// <para>
    /// Most verification types require at least one, and several accept only particular document
    /// types — so a submission that offers none, or offers evidence the type does not accept, is
    /// refused rather than queued. That is Trust's existing rule, applied to this route
    /// unchanged.
    /// </para>
    /// </summary>
    public IReadOnlyList<TrustVerificationEvidence> Evidence { get; init; } = [];

    /// <summary>Free text from the submitter. Truncated by Trust, never interpreted by it.</summary>
    public string? SupportingNotes { get; init; }

    /// <summary>
    /// The caller's own identifier for this submission, and the idempotency key.
    /// <para>
    /// <b>Supply one and a retry is free.</b> Trust records it and, on a second submission
    /// carrying the same value, returns the original verification rather than opening a
    /// second — so a caller that timed out, retried, or replayed a message does not create two
    /// cases for one claim. Omit it and Trust falls back to detecting duplicates by subject,
    /// which is weaker: it cannot tell a deliberate resubmission from an accidental one.
    /// </para>
    /// </summary>
    public Guid? CorrelationId { get; init; }

    /// <summary>
    /// The module raising the claim — <c>Providers</c>, <c>Marketplace</c>. Recorded on the
    /// request, emitted in telemetry, and shown to the reviewer as the claim's provenance.
    /// </summary>
    public string? RequestedBy { get; init; }

    /// <summary>
    /// ISO-3166-1 alpha-2, when the claim is about a particular country community. Routed to the
    /// same field an HTTP submission would set, so a country-scoped review queue sees both.
    /// </summary>
    public string? CountryCode { get; init; }

    /// <summary>
    /// Anything the submitting module wants a reviewer to see that Trust has no field for — a
    /// company number, a practising-certificate reference, a jurisdiction.
    /// <para>
    /// <b>Deliberately opaque to Trust.</b> It is displayed and stored, never parsed and never
    /// branched on. The moment Trust interprets a key here it has learned about a vertical, and
    /// the next vertical needs a Trust change to be verifiable — which is the whole failure this
    /// contract exists to avoid.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
