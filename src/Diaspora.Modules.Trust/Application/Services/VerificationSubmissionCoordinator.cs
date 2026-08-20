using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Extensibility;
using Diaspora.Modules.Trust.Observability;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Trust.Application.Services;

/// <summary>
/// Opens a verification case. The one implementation of that workflow in the module.
/// <para>
/// <b>Extracted in B3.0.2 so the HTTP route and the in-process contract cannot drift.</b> Before
/// this existed there was one submission path, in a command handler, and adding a second entry
/// point would have meant either duplicating the workflow — the provisioning, the type handler's
/// validation, the queueing, the automatic-decision pass, the timeline entry — or having the new
/// contract dispatch a command and inherit an authorisation attribute written for a different
/// caller. Both were worse than moving the workflow down a layer.
/// </para>
/// <para>
/// <b>What it does not decide is the duplicate policy.</b> It reports that a matching case
/// exists; the caller chooses what that means. A member resubmitting from their own screen is
/// making a mistake and gets a 409; a module retrying after a timeout is doing the right thing
/// and gets the original case back. Same detection, different answer, and the difference belongs
/// with the caller rather than buried here.
/// </para>
/// </summary>
public sealed class VerificationSubmissionCoordinator
{
    private readonly IVerificationRequestRepository _verifications;
    private readonly TrustProfileProvisioner _provisioner;
    private readonly ITrustEventRepository _timeline;
    private readonly VerificationTypeHandlerRegistry _handlers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public VerificationSubmissionCoordinator(
        IVerificationRequestRepository verifications,
        TrustProfileProvisioner provisioner,
        ITrustEventRepository timeline,
        VerificationTypeHandlerRegistry handlers,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _verifications = verifications;
        _provisioner = provisioner;
        _timeline = timeline;
        _handlers = handlers;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <summary>
    /// Finds the case this submission would duplicate, if there is one.
    /// <para>
    /// Three checks, narrowing. The correlation identifier is the strongest — an exact "this is
    /// the same submission" from the caller — and is deliberately not filtered by status: a
    /// retry must find the original even after it was decided, or a replayed message reopens a
    /// question that has already been answered.
    /// </para>
    /// </summary>
    public async Task<VerificationRequest?> FindDuplicateAsync(
        VerificationSubmissionInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.CorrelationId is { } correlation)
        {
            var byCorrelation = await _verifications.GetByCorrelationAsync(
                correlation, cancellationToken);

            if (byCorrelation is not null)
            {
                return byCorrelation;
            }
        }

        // A subject-scoped claim is identified by its subject, not by who raised it: two
        // co-owners submitting for one business are making one claim, and one owner submitting
        // for two businesses is making two. Keying on the member would get both backwards.
        if (!string.IsNullOrWhiteSpace(input.SubjectType))
        {
            return await _verifications.GetOpenBySubjectAsync(
                input.Type, input.SubjectType, input.SubjectId, cancellationToken);
        }

        return await _verifications.GetOpenByTypeAsync(
            input.UserId, input.Type, cancellationToken);
    }

    /// <summary>
    /// Runs the submission: opens the request, attaches evidence, lets the type's handler check
    /// it, queues it, gives the type's rule a chance to decide, records the timeline entry and
    /// saves. Transactional, and re-entrant — a module submitting from inside its own
    /// transaction joins it rather than opening a nested one.
    /// </summary>
    public async Task<Result<VerificationRequest>> SubmitAsync(
        VerificationSubmissionInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var handler = _handlers.For(input.Type);

        if (handler is null)
        {
            return Result.Failure<VerificationRequest>(TrustErrors.VerificationTypeUnsupported);
        }

        return await _unitOfWork.ExecuteTransactionalAsync(
            async token =>
            {
                var now = _clock.UtcNow;

                // The member must have a trust profile before anything references it.
                await _provisioner.GetOrCreateBothAsync(input.UserId, token);

                var verification = VerificationRequest.Open(input.UserId, input.Type, now);

                verification.WithSubject(
                    input.SubjectType,
                    input.SubjectId,
                    input.CorrelationId,
                    input.RequestedBy,
                    input.Metadata);

                if (!string.IsNullOrWhiteSpace(input.Nationality)
                    && !string.IsNullOrWhiteSpace(input.ResidenceCountry))
                {
                    verification.WithDiasporaClaim(
                        input.Nationality, input.ResidenceCountry, input.SupportingNotes);
                }

                if (!string.IsNullOrWhiteSpace(input.TargetCountry))
                {
                    verification.WithResidencyClaim(
                        input.TargetCountry, input.ArrivalDate, input.ExpertiseAreas);
                }

                verification.WithSelfie(input.SelfieReference);

                foreach (var submission in input.Documents)
                {
                    verification
                        .AttachDocument(submission.DocumentType, submission.StorageKey, now)
                        .WithFileMetadata(
                            submission.FileName, submission.ContentType, submission.SizeInBytes)
                        .WithDocumentDetails(
                            submission.DocumentNumber,
                            submission.IssuingCountry,
                            submission.IssuedOn,
                            submission.ExpiresOn);
                }

                foreach (var evidence in input.Evidence)
                {
                    verification.AttachMediaEvidence(evidence.MediaId, evidence.DocumentType, now);
                }

                // The handler checks its own type's requirements before anything is queued, so
                // a submission missing its selfie is refused here rather than wasting a review
                // slot.
                var problems = handler.Validate(verification);

                if (problems.Count > 0)
                {
                    return Result.Failure<VerificationRequest>(
                        Error.Validation(
                            TrustErrorCodes.VerificationTypeUnsupported,
                            string.Join(" ", problems)));
                }

                await _verifications.AddAsync(verification, token);

                verification.Queue(now, handler.ReviewSla);

                ApplyAutomaticDecision(handler, verification, now);

                await _timeline.AddAsync(
                    TrustEvent.Record(
                        input.UserId,
                        TrustEventTypes.VerificationSubmitted,
                        $"{Humanize(input.Type)} verification submitted",
                        now,
                        "Your documents were sent for compliance review.",
                        TrustEventTypes.IconFor(TrustEventTypes.VerificationSubmitted),
                        input.TargetCountry),
                    token);

                await _unitOfWork.SaveChangesAsync(token);

                TrustTelemetrySource.VerificationsSubmitted.Add(
                    1,
                    new KeyValuePair<string, object?>("type", input.Type.ToString()),
                    new KeyValuePair<string, object?>(
                        "subject", input.SubjectType ?? TrustSubmissionDefaults.MemberSubject),
                    new KeyValuePair<string, object?>(
                        "source", input.RequestedBy ?? TrustSubmissionDefaults.SelfServiceSource));

                return Result.Success(verification);
            },
            cancellationToken);
    }

    /// <summary>
    /// Gives the type's rule a chance to decide without a human. Most defer.
    /// <para>
    /// Automatic <em>rejection</em> always applies: an expired document or a nationality that
    /// matches the country of residence are facts, not judgements, and queuing them wastes a
    /// reviewer on a foregone conclusion. Automatic <em>approval</em> is gated on the handler
    /// opting in, because a type that unlocks publishing must never have a path to a grant that
    /// no human ever looked at.
    /// </para>
    /// </summary>
    private static void ApplyAutomaticDecision(
        IVerificationTypeHandler handler, VerificationRequest verification, DateTimeOffset now)
    {
        var decision = handler.Evaluate(verification, now);

        switch (decision.Kind)
        {
            case AutomaticDecisionKind.Approve when handler.SupportsAutomaticApproval:
                verification.AutoApprove(
                    decision.RuleName,
                    decision.Details,
                    now,
                    handler.GrantLifetime is { } lifetime ? now.Add(lifetime) : null);
                break;

            case AutomaticDecisionKind.Reject:
                verification.AutoReject(
                    decision.RuleName,
                    decision.Reason ?? VerificationRejectionReason.INVALID_DOCUMENT,
                    decision.Details,
                    now);
                break;
        }
    }

    /// <summary>"CommunityGuide" reads better to a member as "Community Guide".</summary>
    private static string Humanize(VerificationType type) =>
        System.Text.RegularExpressions.Regex.Replace(
            type.ToString(), "(?<!^)([A-Z])", " $1");
}

/// <summary>Telemetry dimension values for submissions that name no subject or module.</summary>
public static class TrustSubmissionDefaults
{
    /// <summary>The subject dimension for a member verifying themselves.</summary>
    public const string MemberSubject = "Member";

    /// <summary>The source dimension for a member's own submission through the HTTP route.</summary>
    public const string SelfServiceSource = "self-service";
}

/// <summary>
/// Everything the submission workflow needs, from whichever caller supplied it.
/// <para>
/// Trust's own types rather than the published contract's: this is the inside of the module, and
/// the mapping from <c>TrustVerificationSubmissionRequest</c> happens once, at the edge, where a
/// bad type name becomes a refusal instead of an exception.
/// </para>
/// </summary>
public sealed record VerificationSubmissionInput
{
    public required UserId UserId { get; init; }

    public required VerificationType Type { get; init; }

    public string? Nationality { get; init; }

    public string? ResidenceCountry { get; init; }

    public string? TargetCountry { get; init; }

    public DateOnly? ArrivalDate { get; init; }

    public string? SupportingNotes { get; init; }

    public string? SelfieReference { get; init; }

    public IReadOnlyList<string>? ExpertiseAreas { get; init; }

    /// <summary>Directly uploaded evidence, from the member's own wizard.</summary>
    public IReadOnlyList<Verifications.DocumentSubmission> Documents { get; init; } = [];

    /// <summary>Evidence held by Media, from a module's submission. References, never URLs.</summary>
    public IReadOnlyList<MediaEvidence> Evidence { get; init; } = [];

    /// <summary>Null means the member is the subject — every pre-B3.0.2 submission.</summary>
    public string? SubjectType { get; init; }

    public Guid? SubjectId { get; init; }

    public Guid? CorrelationId { get; init; }

    public string? RequestedBy { get; init; }

    public string? Metadata { get; init; }
}

/// <summary>
/// One Media reference offered as evidence, with the type the submitting module says it is.
/// <para>
/// Trust's own enum rather than the contract's string: the translation happens once, at the
/// edge, so an unrecognised type name becomes a refusal there instead of an exception here.
/// </para>
/// </summary>
public readonly record struct MediaEvidence(Guid MediaId, VerificationDocumentType DocumentType);
