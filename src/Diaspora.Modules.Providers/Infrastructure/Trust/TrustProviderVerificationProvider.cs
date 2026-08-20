using Diaspora.Contracts.Trust.V1.Submission;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Extensibility.Verification;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Infrastructure.Trust;

/// <summary>
/// Routes a provider verification claim to the Trust module.
/// <para>
/// <b>The shipped implementation, and it is deliberately last</b> — order 1000 — so any
/// contributed provider that can decide a claim without a human gets there first. A national
/// companies register that confirms a company number in a second is a better answer than a
/// review queue, and this exists to be the fallback rather than the ceiling.
/// </para>
/// <para>
/// <b>It returns <see cref="ProviderVerificationOutcome.Deferred"/>, never a grant.</b> Trust
/// owns the decision; B7 records that a claim was raised and waits for the integration event.
/// That separation is ADR-002's, and it is what keeps a verified badge on a money-transfer
/// business meaningful.
/// </para>
/// <para>
/// <b>B3.0.2 replaced the stub here.</b> Until then this generated a correlating identifier and
/// returned <c>Deferred</c> without calling anything, because Trust published no way in — so the
/// identifier B7 stored matched no case Trust held, and no decision could ever find the claim.
/// It now submits through <see cref="ITrustVerificationSubmission"/> and returns the identifier
/// Trust actually opened, which is what the four outcome handlers have always matched on.
/// </para>
/// <para>
/// The contract lives in <c>Diaspora.Contracts</c>, so this module still holds no reference to
/// <c>Diaspora.Modules.Trust</c> — the architecture test that asserts it passes unchanged.
/// </para>
/// </summary>
public sealed class TrustProviderVerificationProvider : IProviderVerificationProvider
{
    /// <summary>
    /// The submission contract, when Trust is loaded.
    /// <para>
    /// Optional deliberately. A deployment without the Trust module has no verification engine
    /// at all, and the honest answer there is "nothing decided this" — which falls through to the
    /// registry's <c>provider-unavailable</c> and leaves no claim recorded. Requiring it would
    /// instead fail the container at startup, turning a module the deployment chose not to run
    /// into a boot failure.
    /// </para>
    /// </summary>
    private readonly ITrustVerificationSubmission? _submission;

    public TrustProviderVerificationProvider(ITrustVerificationSubmission? submission = null) =>
        _submission = submission;

    public string Module => ProvidersModuleMetadata.Name;

    public int Order => 1_000;

    /// <summary>Every kind. Trust is the universal fallback, which is why it runs last.</summary>
    public bool Handles(ProviderVerificationKind kind) => true;

    public async Task<Result<ProviderVerificationOutcome>> SubmitAsync(
        ProviderVerificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_submission is null)
        {
            return Result.Success(ProviderVerificationOutcome.Undecided);
        }

        var submitted = await _submission.SubmitAsync(
            new TrustVerificationSubmissionRequest
            {
                VerificationType = ProviderVerificationTypeMap.ToTrust(request.Kind),
                SubmittedBy = request.SubmittedBy,
                Subject = SubjectFor(request),
                Evidence = [.. request.EvidenceMediaIds.Select(
                    mediaId => TrustVerificationEvidence.Document(
                        mediaId, EvidenceTypeFor(request.Kind)))],
                CountryCode = request.CountryCode,
                RequestedBy = ProvidersModuleMetadata.Name,
                SupportingNotes = null,

                // No correlation identifier, and that is the right call rather than an omission.
                // Trust's subject check already makes a retry safe — it finds the *open* case for
                // this provider and kind — while a correlation identifier derived from the claim
                // would also match a case that was already rejected, and refuse the resubmission
                // the member is entitled to make after fixing their evidence.
                CorrelationId = null,
                Metadata = DescribeClaim(request)
            },
            cancellationToken);

        if (submitted.IsFailure)
        {
            return Result.Failure<ProviderVerificationOutcome>(submitted.Error);
        }

        // Deferred whatever Trust says, including when a type handler's rule already decided.
        // B7 learns outcomes from integration events and from nowhere else: reading a decision
        // out of the submission response would be a second path to a verified badge, and the
        // whole point of the projection is that there is exactly one.
        return Result.Success(
            ProviderVerificationOutcome.Deferred(submitted.Value.VerificationId));
    }

    /// <summary>
    /// What Trust should treat as the thing under review.
    /// <para>
    /// A certification is its own subject rather than the provider's, because a provider may
    /// hold several and they are judged separately. Sharing the provider's subject would make the
    /// second certification look like a duplicate of the first — both map onto Trust's
    /// <c>Professional</c> type, which has no finer distinction and does not need one once the
    /// subject carries it.
    /// </para>
    /// </summary>
    private static TrustVerificationSubject SubjectFor(ProviderVerificationRequest request) =>
        request.Kind == ProviderVerificationKind.Certification && request.CertificationId is { } certification
            ? TrustVerificationSubject.For(ProviderSubjectTypes.Certification, certification)
            : TrustVerificationSubject.For(
                TrustVerificationSubjectTypes.Provider, request.ProviderId);

    /// <summary>
    /// What B7 says its evidence is.
    /// <para>
    /// Trust's verification types accept particular document types and refuse the rest — a
    /// <c>Business</c> claim wants a registration document, not an unlabelled file — so evidence
    /// submitted without a type would be refused for exactly the kinds providers care about.
    /// B7 knows what it asked the owner to upload, because its own screens asked for it.
    /// </para>
    /// <para>
    /// These are Trust's <c>VerificationDocumentType</c> names, as strings, for the same reason
    /// the type map beside this uses strings: the published contract is a string on both sides,
    /// so one vocabulary rather than two that can drift.
    /// </para>
    /// </summary>
    private static string EvidenceTypeFor(ProviderVerificationKind kind) => kind switch
    {
        ProviderVerificationKind.Business => "BusinessRegistration",
        ProviderVerificationKind.Organization => "OrganizationDocument",

        // Embassy endorsements arrive as official correspondence; Trust's Embassy handler
        // accepts government documents and Other, and this is the former where it applies.
        ProviderVerificationKind.Embassy => "GovernmentImmigrationDocuments",

        // A practising certificate is an employment-style credential to Trust's Professional
        // handler, which accepts EmploymentLetter, OccupationPermit and Other.
        ProviderVerificationKind.Professional => "EmploymentLetter",
        ProviderVerificationKind.Certification => "Other",

        ProviderVerificationKind.Identity => "NationalId",

        // A document claim is exactly what it says and nothing narrower.
        _ => "Other"
    };

    /// <summary>
    /// The facts a reviewer needs that Trust has no field for.
    /// <para>
    /// A company number and a jurisdiction are the whole job for a business claim — a reviewer
    /// opens a register and checks. Trust stores this as text and never parses it, which is what
    /// keeps it from having to know what a company number is.
    /// </para>
    /// </summary>
    private static Dictionary<string, string> DescribeClaim(ProviderVerificationRequest request)
    {
        var described = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["providerId"] = request.ProviderId.ToString(),
            ["providerName"] = request.ProviderName,
            ["providerType"] = request.ProviderType.ToString(),
            ["kind"] = request.Kind.ToString()
        };

        Add(described, "legalName", request.LegalName);
        Add(described, "registrationNumber", request.RegistrationNumber);
        Add(described, "registrationJurisdiction", request.RegistrationJurisdiction);
        Add(described, "taxIdentifier", request.TaxIdentifier);
        Add(described, "certificationId", request.CertificationId?.ToString());
        Add(described, "certificationName", request.CertificationName);
        Add(described, "certificationIssuer", request.CertificationIssuer);
        Add(described, "certificationReference", request.CertificationReference);

        return described;
    }

    private static void Add(Dictionary<string, string> target, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[key] = value;
        }
    }
}

/// <summary>The subject names B7 owns, beyond the ones the platform ships.</summary>
public static class ProviderSubjectTypes
{
    /// <summary>One credential a provider holds, judged on its own.</summary>
    public const string Certification = "ProviderCertification";
}
