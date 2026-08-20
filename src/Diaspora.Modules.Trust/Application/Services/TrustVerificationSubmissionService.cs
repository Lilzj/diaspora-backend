using System.Globalization;
using Diaspora.Contracts.Trust.V1.Submission;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Trust.Application.Services;

/// <summary>
/// Trust's side of the published submission contract.
/// <para>
/// <b>The edge, and only the edge.</b> It translates a published request into the module's own
/// vocabulary, applies the two rules that are specific to an in-process caller — who may submit
/// for whom, and what a repeat submission means — and hands everything else to
/// <see cref="VerificationSubmissionCoordinator"/>, which is the same code the member's own HTTP
/// route runs. A claim raised by Marketplace and a claim raised from the verification wizard
/// reach the reviewer identically, because after this class they are the same call.
/// </para>
/// <para>
/// <b>It cannot approve anything.</b> There is no path from here to
/// <c>Approve</c>, <c>Reject</c>, <c>Revoke</c>, <c>Expire</c> or a badge — the surface is
/// "open a case", and the strongest thing it can return is an identifier and a status. Automatic
/// decisions still happen inside the coordinator on the type handler's authority, exactly as they
/// do for a member's own submission, which is the one place a submission can become a decision
/// without a human and it is Trust's rule, not the caller's.
/// </para>
/// </summary>
public sealed class TrustVerificationSubmissionService : ITrustVerificationSubmission
{
    private readonly VerificationSubmissionCoordinator _submissions;
    private readonly ICurrentUser _currentUser;

    public TrustVerificationSubmissionService(
        VerificationSubmissionCoordinator submissions, ICurrentUser currentUser)
    {
        _submissions = submissions;
        _currentUser = currentUser;
    }

    public async Task<Result<TrustVerificationSubmissionResult>> SubmitAsync(
        TrustVerificationSubmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.SubmittedBy == Guid.Empty)
        {
            return Failure(TrustErrors.NotAuthenticated);
        }

        // A module may submit for the member who is acting, and for nobody else. Without this a
        // vertical could open a verification against any member it could name, and a rejection
        // — which is a fact recorded against a person's standing — would be reachable by a
        // caller that person never interacted with.
        //
        // No current user means a background or system context. Allowed, because a sweeper or a
        // migration has no acting member and refusing it would make those callers impossible;
        // the submitting module is recorded either way.
        if (_currentUser.UserId is { } caller && caller.Value != request.SubmittedBy)
        {
            TrustTelemetrySource.SubmissionsRejected.Add(
                1,
                new KeyValuePair<string, object?>("reason", "submitter-mismatch"),
                new KeyValuePair<string, object?>("source", Source(request)));

            return Failure(TrustErrors.SubmissionNotPermitted);
        }

        if (!Enum.TryParse<VerificationType>(request.VerificationType, ignoreCase: true, out var type))
        {
            TrustTelemetrySource.SubmissionsRejected.Add(
                1,
                new KeyValuePair<string, object?>("reason", "type-unsupported"),
                new KeyValuePair<string, object?>("source", Source(request)));

            return Failure(TrustErrors.VerificationTypeUnsupported);
        }

        var subject = request.Subject ?? TrustVerificationSubject.Member;

        var evidence = new List<MediaEvidence>();

        foreach (var offered in request.Evidence ?? [])
        {
            // An unrecognised document type is refused rather than quietly downgraded to Other:
            // silently changing what a module said its evidence is would show a reviewer the
            // wrong thing, and the type is what decides whether the claim is even acceptable.
            if (string.IsNullOrWhiteSpace(offered.DocumentType))
            {
                evidence.Add(new MediaEvidence(offered.MediaId, VerificationDocumentType.Other));
                continue;
            }

            if (!Enum.TryParse<VerificationDocumentType>(
                    offered.DocumentType, ignoreCase: true, out var documentType))
            {
                TrustTelemetrySource.SubmissionsRejected.Add(
                    1,
                    new KeyValuePair<string, object?>("reason", "document-type-unsupported"),
                    new KeyValuePair<string, object?>("source", Source(request)));

                return Failure(TrustErrors.DocumentTypeUnsupported);
            }

            evidence.Add(new MediaEvidence(offered.MediaId, documentType));
        }

        var input = new VerificationSubmissionInput
        {
            UserId = UserId.From(request.SubmittedBy),
            Type = type,
            TargetCountry = request.CountryCode,
            SupportingNotes = request.SupportingNotes,
            Evidence = evidence,

            // "Member" is stored as no subject at all, so a module submitting on a member's own
            // behalf produces a row indistinguishable from the wizard's — same duplicate rule,
            // same queue, same everything. The alternative would be two spellings of one fact.
            SubjectType = IsMemberSubject(subject) ? null : subject.SubjectType,
            SubjectId = IsMemberSubject(subject) ? null : subject.SubjectId,
            CorrelationId = request.CorrelationId,
            RequestedBy = request.RequestedBy,
            Metadata = FormatMetadata(request.Metadata)
        };

        TrustTelemetrySource.SubmissionsStarted.Add(
            1,
            new KeyValuePair<string, object?>("type", type.ToString()),
            new KeyValuePair<string, object?>("subject", input.SubjectType ?? TrustSubmissionDefaults.MemberSubject),
            new KeyValuePair<string, object?>("source", Source(request)));

        // A repeat is an answer, not an error. The caller retried, replayed a message, or asked
        // twice for a claim already in flight; all three want the original case back rather than
        // a second reviewer looking at the same evidence.
        var existing = await _submissions.FindDuplicateAsync(input, cancellationToken);

        if (existing is not null)
        {
            TrustTelemetrySource.SubmissionsDuplicate.Add(
                1,
                new KeyValuePair<string, object?>("type", type.ToString()),
                new KeyValuePair<string, object?>("source", Source(request)));

            return Result.Success(Describe(existing, isDuplicate: true));
        }

        var submitted = await _submissions.SubmitAsync(input, cancellationToken);

        if (submitted.IsFailure)
        {
            TrustTelemetrySource.SubmissionsRejected.Add(
                1,
                new KeyValuePair<string, object?>("reason", submitted.Error.Code),
                new KeyValuePair<string, object?>("source", Source(request)));

            return Failure(submitted.Error);
        }

        TrustTelemetrySource.SubmissionsAccepted.Add(
            1,
            new KeyValuePair<string, object?>("type", type.ToString()),
            new KeyValuePair<string, object?>("subject", input.SubjectType ?? TrustSubmissionDefaults.MemberSubject),
            new KeyValuePair<string, object?>("source", Source(request)));

        return Result.Success(Describe(submitted.Value, isDuplicate: false));
    }

    private static bool IsMemberSubject(TrustVerificationSubject subject) =>
        string.IsNullOrWhiteSpace(subject.SubjectType)
            || string.Equals(
                subject.SubjectType,
                TrustVerificationSubjectTypes.Member,
                StringComparison.OrdinalIgnoreCase);

    private static TrustVerificationSubmissionResult Describe(
        VerificationRequest verification, bool isDuplicate) =>
        new()
        {
            VerificationId = verification.Id.Value,
            Status = verification.Status.ToString(),
            IsDuplicate = isDuplicate,
            SubmittedAt = verification.SubmittedAt,
            ReviewDueAt = verification.ReviewDueAt
        };

    private static Result<TrustVerificationSubmissionResult> Failure(Error error) =>
        Result.Failure<TrustVerificationSubmissionResult>(error);

    private static string Source(TrustVerificationSubmissionRequest request) =>
        string.IsNullOrWhiteSpace(request.RequestedBy) ? "unknown" : request.RequestedBy;

    /// <summary>
    /// Flattens the caller's metadata for a reviewer to read.
    /// <para>
    /// Stored as text and never parsed back. Trust displays it and has no opinion about it —
    /// the moment a key here is branched on, Trust has learned about a vertical and the next
    /// vertical needs a Trust change before it can be verified.
    /// </para>
    /// </summary>
    private static string? FormatMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return null;
        }

        return string.Join(
            "; ",
            metadata
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => string.Create(
                    CultureInfo.InvariantCulture, $"{entry.Key}={entry.Value}")));
    }
}
