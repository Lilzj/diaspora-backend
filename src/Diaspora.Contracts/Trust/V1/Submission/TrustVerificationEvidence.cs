namespace Diaspora.Contracts.Trust.V1.Submission;

/// <summary>
/// One piece of evidence offered with a submission.
/// <para>
/// <b>A reference and a claim about what it is.</b> The reference is a Media identifier — never a
/// URL, never bytes — and the claim is what the submitting module says the asset shows. Both are
/// needed: Trust's verification types accept particular kinds of document, so evidence that
/// cannot describe itself is evidence Trust must refuse.
/// </para>
/// <para>
/// The module supplies the type because the module is the only party that knows. Trust has not
/// opened the file, and a type inferred from a Media identifier would be Trust inventing a fact
/// about evidence it has not looked at.
/// </para>
/// </summary>
public sealed record TrustVerificationEvidence
{
    /// <summary>
    /// The Media asset. Media owns the file, its storage, its lifecycle and who may read it;
    /// Trust records only that it was offered.
    /// </summary>
    public required Guid MediaId { get; init; }

    /// <summary>
    /// Trust's <c>VerificationDocumentType</c> name — <c>BusinessRegistration</c>,
    /// <c>OrganizationDocument</c>, <c>EmploymentLetter</c>, <c>Passport</c>, and so on.
    /// <para>
    /// Null means <c>Other</c>, which several types accept and several do not. A submission whose
    /// evidence a type does not accept is refused with the type's own message rather than queued
    /// for a reviewer who would refuse it anyway.
    /// </para>
    /// </summary>
    public string? DocumentType { get; init; }

    /// <summary>A reference with no stated type. Accepted where <c>Other</c> is.</summary>
    public static TrustVerificationEvidence Media(Guid mediaId) => new() { MediaId = mediaId };

    /// <summary>A reference the submitting module can name.</summary>
    public static TrustVerificationEvidence Document(Guid mediaId, string documentType) =>
        new() { MediaId = mediaId, DocumentType = documentType };
}
