using Diaspora.Contracts.Trust.V1.Submission;
using Diaspora.Modules.Guides.Extensibility.Verification;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Infrastructure.Trust;

/// <summary>
/// Routes a guide accuracy claim to the Trust module.
/// <para>
/// <b>The shipped provider, and deliberately last</b> — order 1000 — so a deployment that
/// contributes a provider able to decide without a human gets there first. A government content
/// programme confirming its own circular in a second is a better answer than a review queue, and
/// this exists to be the fallback rather than the ceiling.
/// </para>
/// <para>
/// <b>It returns <see cref="GuideVerificationOutcome.Deferred"/>, never a grant.</b> Trust owns
/// the decision; B8 records that a claim was raised and waits for the integration event. That
/// separation is ADR-002's, and it is what keeps a verified mark on a visa guide meaningful.
/// </para>
/// <para>
/// <b>The claim submits as <c>CommunityGuide</c></b> — see
/// <see cref="GuideVerificationTypeMap"/> for why that type and not <c>Contributor</c>. Evidence
/// always carries a <c>DocumentType</c>: Trust's type handlers refuse untyped evidence before a
/// reviewer sees it, which is the B3.0.2 trap.
/// </para>
/// <para>
/// The submission contract lives in <c>Diaspora.Contracts</c>, so this module still holds no
/// reference to <c>Diaspora.Modules.Trust</c> — the architecture test that asserts it passes
/// unchanged.
/// </para>
/// </summary>
public sealed class TrustGuideVerificationProvider : IGuideVerificationProvider
{
    /// <summary>
    /// The submission contract, when Trust is loaded.
    /// <para>
    /// Optional deliberately. A deployment without the Trust module has no verification engine at
    /// all, and the honest answer there is a failed result the registry turns into
    /// <c>guides.verification.unavailable</c> — leaving no claim recorded rather than a pending
    /// mark no system is holding. Requiring the dependency would instead fail the container at
    /// startup, turning a module the deployment chose not to run into a boot failure.
    /// </para>
    /// </summary>
    private readonly ITrustVerificationSubmission? _submission;

    public TrustGuideVerificationProvider(ITrustVerificationSubmission? submission = null) =>
        _submission = submission;

    public string Module => GuidesModuleMetadata.Name;

    public int Order => 1_000;

    public async Task<Result<GuideVerificationOutcome>> SubmitAsync(
        GuideVerificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_submission is null)
        {
            return Result.Failure<GuideVerificationOutcome>(
                Domain.Errors.GuideErrors.VerificationUnavailable);
        }

        var submitted = await _submission.SubmitAsync(
            new TrustVerificationSubmissionRequest
            {
                VerificationType = GuideVerificationTypeMap.GuideVerificationType,

                // Trust reviews people. The submitter is accountable for the claim even though
                // the subject is a document, which is exactly what the subject model separates.
                SubmittedBy = request.SubmittedBy,
                Subject = TrustVerificationSubject.For("Guide", request.GuideId),
                Evidence = [.. request.Evidence.Select(ToTrustEvidence)],
                CountryCode = request.CountryCode,
                RequestedBy = GuidesModuleMetadata.Name,
                SupportingNotes = request.Notes,

                // The guide id is the idempotency key, so a timed-out submission retried by the
                // member — or a replayed message — returns the original case rather than opening
                // a second. A guide is one subject with one live claim about its accuracy, unlike
                // a provider that may resubmit a different kind of proof.
                CorrelationId = request.GuideId,
                Metadata = DescribeClaim(request)
            },
            cancellationToken);

        if (submitted.IsFailure)
        {
            return Result.Failure<GuideVerificationOutcome>(submitted.Error);
        }

        // Deferred whatever Trust says, including when a type handler's rule already decided.
        // B8 learns outcomes from integration events and from nowhere else: reading a decision out
        // of the submission response would be a second path to a verified mark, and the whole
        // point of the projection is that there is exactly one.
        return Result.Success(
            GuideVerificationOutcome.Deferred(submitted.Value.VerificationId));
    }

    /// <summary>
    /// Types every reference, because untyped evidence is refused.
    /// <para>
    /// Trust's <c>CommunityGuide</c> handler accepts <c>GuideEvidence</c> and <c>Other</c>; a
    /// caller that named its own type keeps it, and everything else is stamped
    /// <c>GuideEvidence</c>. B8 knows what it asked the author to upload, because its own screens
    /// asked for it.
    /// </para>
    /// </summary>
    private static TrustVerificationEvidence ToTrustEvidence(GuideVerificationEvidence evidence) =>
        TrustVerificationEvidence.Document(
            evidence.MediaId,
            string.IsNullOrWhiteSpace(evidence.DocumentType)
                ? GuideVerificationTypeMap.DefaultEvidenceDocumentType
                : evidence.DocumentType);

    /// <summary>
    /// The facts a reviewer needs that Trust has no field for.
    /// <para>
    /// A reviewer checking a guide opens it and reads it, so the slug, the category and the
    /// version under review are the whole job. Trust stores this as text and never parses it,
    /// which is what keeps it from having to learn what a guide is.
    /// </para>
    /// </summary>
    private static Dictionary<string, string> DescribeClaim(GuideVerificationRequest request)
    {
        var described = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["guideId"] = request.GuideId.ToString(),
            ["slug"] = request.Slug,
            ["title"] = request.Title,
            ["categoryKey"] = request.CategoryKey,
            ["language"] = request.Language,
            ["version"] = request.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["authorId"] = request.AuthorId.ToString()
        };

        if (!string.IsNullOrWhiteSpace(request.CountryCode))
        {
            described["countryCode"] = request.CountryCode;
        }

        return described;
    }
}
