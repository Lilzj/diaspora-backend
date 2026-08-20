using System.Globalization;
using Diaspora.Contracts.Trust.V1.Submission;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Extensibility.Intelligence;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Infrastructure.Trust;

/// <summary>
/// Routes a claim that a body is official to the Trust module.
/// <para>
/// <b>The shipped provider, and deliberately last</b> — order 1000 — so a deployment that contributes
/// one able to decide without a human gets there first. A foreign ministry's own accredited-missions
/// list confirming a consulate in a second is a better answer than a review queue, and this exists to
/// be the fallback rather than the ceiling.
/// </para>
/// <para>
/// <b>It returns a deferred outcome, never a grant.</b> Trust owns the decision; B8.2 records that a
/// claim was raised and waits for the integration event. That separation is ADR-002's, and it is what
/// keeps a verified badge on an immigration department meaningful.
/// </para>
/// <para>
/// <b>The subject identifier is the idempotency key.</b> An authority is one subject with one live
/// claim about its legitimacy, so a timed-out submission retried by an editor — or a replayed message
/// — returns the original case rather than opening a second, which would leave two reviewers deciding
/// the same question and two decisions arriving out of order at one projection.
/// </para>
/// <para>
/// The submission contract lives in <c>Diaspora.Contracts</c>, so this module still holds no reference
/// to <c>Diaspora.Modules.Trust</c> — the architecture test that asserts it passes unchanged.
/// </para>
/// </summary>
public sealed class TrustAuthorityVerificationProvider : IAuthorityVerificationProvider
{
    /// <summary>
    /// The submission contract, when Trust is loaded.
    /// <para>
    /// Optional deliberately. A deployment without the Trust module has no verification engine at
    /// all, and the honest answer there is a failed result the registry turns into
    /// <c>intelligence.verification.unavailable</c> — leaving no claim recorded rather than a pending
    /// badge no system is holding. Requiring the dependency would instead fail the container at
    /// startup, turning a module the deployment chose not to run into a boot failure.
    /// </para>
    /// </summary>
    private readonly ITrustVerificationSubmission? _submission;

    public TrustAuthorityVerificationProvider(ITrustVerificationSubmission? submission = null) =>
        _submission = submission;

    public string Module => GuidesModuleMetadata.Name;

    public int Order => 1_000;

    /// <summary>Every country. The shipped provider is the fallback, so it refuses nothing.</summary>
    public bool Handles(string countryCode) => true;

    public async Task<Result<AuthorityVerificationOutcome>> SubmitAsync(
        AuthorityVerificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_submission is null)
        {
            return Result.Failure<AuthorityVerificationOutcome>(
                IntelligenceErrors.VerificationUnavailable);
        }

        var subject = request.SubjectKind == AuthoritySubjectKind.Embassy
            ? "Embassy"
            : "IssuingAuthority";

        var submitted = await _submission.SubmitAsync(
            new TrustVerificationSubmissionRequest
            {
                VerificationType = AuthorityVerificationTypeMap.AuthorityVerificationType,

                // Trust reviews people. The submitter is accountable for the claim even though the
                // subject is an institution, which is exactly what the subject model separates.
                SubmittedBy = request.SubmittedBy,
                Subject = TrustVerificationSubject.For(subject, request.SubjectId),
                Evidence = [.. request.Evidence.Select(ToTrustEvidence)],
                CountryCode = request.CountryCode,
                RequestedBy = GuidesModuleMetadata.Name,
                SupportingNotes = request.Notes,
                CorrelationId = request.SubjectId,
                Metadata = DescribeClaim(request)
            },
            cancellationToken);

        return submitted.IsFailure
            ? Result.Failure<AuthorityVerificationOutcome>(submitted.Error)
            : Result.Success(
                AuthorityVerificationOutcome.Deferred(submitted.Value.VerificationId));
    }

    /// <summary>
    /// Types every reference, because untyped evidence is refused.
    /// <para>
    /// Trust's <c>Embassy</c> handler accepts <c>GovernmentImmigrationDocuments</c>,
    /// <c>OrganizationDocument</c> and <c>Other</c>; a caller that named its own type keeps it, and
    /// everything else is stamped with the first. B8.2 knows what its own screens asked an editor to
    /// upload.
    /// </para>
    /// </summary>
    private static TrustVerificationEvidence ToTrustEvidence(
        AuthorityVerificationEvidence evidence) =>
        TrustVerificationEvidence.Document(
            evidence.MediaId,
            string.IsNullOrWhiteSpace(evidence.DocumentType)
                ? AuthorityVerificationTypeMap.DefaultEvidenceDocumentType
                : evidence.DocumentType);

    /// <summary>
    /// The facts a reviewer needs that Trust has no field for.
    /// <para>
    /// A reviewer checking an institution opens its website and compares the name against an official
    /// register, so the name, the type and the site are the whole job. Trust stores this as text and
    /// never parses it, which is what keeps it from having to learn what an issuing authority is.
    /// </para>
    /// </summary>
    private static Dictionary<string, string> DescribeClaim(AuthorityVerificationRequest request)
    {
        var described = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["subjectKind"] = request.SubjectKind.ToString(),
            ["subjectId"] = request.SubjectId.ToString(),
            ["name"] = request.Name,
            ["authorityType"] = request.AuthorityType.ToString(),
            ["countryCode"] = request.CountryCode
        };

        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            described["website"] = request.Website;
        }

        described["evidenceCount"] =
            request.Evidence.Count.ToString(CultureInfo.InvariantCulture);

        return described;
    }
}
