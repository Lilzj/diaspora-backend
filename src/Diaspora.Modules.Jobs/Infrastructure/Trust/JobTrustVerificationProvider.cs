using Diaspora.Contracts.Trust.V1.Submission;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Jobs.Infrastructure.Trust;

/// <summary>
/// The one class in B9 that talks to Trust.
/// <para>
/// <b>It reaches Trust through the published <c>ITrustVerificationSubmission</c> contract in
/// <c>Diaspora.Contracts</c>, and holds no reference to the Trust module.</b> That is B3.0.2's
/// whole point, and an architecture test asserts it: consuming the contract must not mean
/// referencing the module that decides.
/// </para>
/// <para>
/// <b>What it may do is ask.</b> Approval, rejection, revocation, badge assignment and outcome
/// publication are Trust's alone and are not reachable from here — which is why the method
/// returns a case identifier rather than a decision, and why nothing in this file can set a
/// <c>JobTrustMark</c> to verified.
/// </para>
/// <para>
/// <b>The dependency is optional by design.</b> A deployment that does not load Trust has no
/// verification engine at all, so the contract is injected nullable and a missing implementation
/// degrades to "unavailable" rather than throwing. B7's provider does exactly the same, and it is
/// the difference between a module that cannot be verified and a platform that cannot start.
/// </para>
/// </summary>
public sealed class JobTrustVerificationProvider
{
    private readonly ITrustVerificationSubmission? _submission;
    private readonly ILogger<JobTrustVerificationProvider> _logger;

    public JobTrustVerificationProvider(
        ILogger<JobTrustVerificationProvider> logger,
        ITrustVerificationSubmission? submission = null)
    {
        _submission = submission;
        _logger = logger;
    }

    /// <summary>Whether this deployment has a verification engine at all.</summary>
    public bool IsAvailable => _submission is not null;

    /// <summary>
    /// Asks Trust to verify an employer.
    /// <para>
    /// Submitted as <c>Business</c> against the employer's own identifier, so two employers owned
    /// by one member are distinguishable — which is exactly the gap B3.0.2's subject model
    /// closed. The correlation identifier is the employer's, so a retry after a timeout returns
    /// the original case rather than opening a second.
    /// </para>
    /// <para>
    /// The registration number and legal name go in the opaque metadata bag, where Trust displays
    /// them to a reviewer and never parses them. The moment Trust interpreted a key there it
    /// would have learned what an employer is, and the next vertical would need a Trust change.
    /// </para>
    /// </summary>
    public async Task<Result<Guid>> SubmitEmployerAsync(
        EmployerProfile employer,
        UserId requestedBy,
        IReadOnlyList<Guid> evidenceMediaIds,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employer);
        ArgumentNullException.ThrowIfNull(requestedBy);
        ArgumentNullException.ThrowIfNull(evidenceMediaIds);

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["employerName"] = employer.Identity.VerifiableName,
            ["organizationType"] = employer.Identity.OrganizationType.ToString(),
            ["slug"] = employer.Slug.Value
        };

        if (employer.Identity.RegistrationNumber is { Length: > 0 } registration)
        {
            metadata["registrationNumber"] = registration;
        }

        return await SubmitAsync(
            new TrustVerificationSubmissionRequest
            {
                VerificationType = "Business",
                SubmittedBy = requestedBy.Value,
                Subject = TrustVerificationSubject.For(
                    TrustVerificationSubjectTypes.Business, employer.Id.Value),
                Evidence = [.. evidenceMediaIds.Select(TrustVerificationEvidence.Media)],
                SupportingNotes = notes,
                CorrelationId = employer.Id.Value,
                RequestedBy = JobsModuleMetadata.Name,
                CountryCode = employer.PrimaryCountryCode,
                Metadata = metadata
            },
            cancellationToken);
    }

    /// <summary>
    /// Asks Trust to verify a posting.
    /// <para>
    /// Submitted as <c>Organization</c> against the posting's identifier. A posting-level claim
    /// is a narrower thing than an employer-level one — it says "this particular advert is what
    /// it appears to be" — and keeping the two subjects distinct is what lets a verified employer
    /// still have an unverified posting reviewed.
    /// </para>
    /// </summary>
    public async Task<Result<Guid>> SubmitPostingAsync(
        JobPosting posting,
        UserId requestedBy,
        IReadOnlyList<Guid> evidenceMediaIds,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(posting);
        ArgumentNullException.ThrowIfNull(requestedBy);
        ArgumentNullException.ThrowIfNull(evidenceMediaIds);

        return await SubmitAsync(
            new TrustVerificationSubmissionRequest
            {
                VerificationType = "Organization",
                SubmittedBy = requestedBy.Value,
                Subject = TrustVerificationSubject.For(
                    TrustVerificationSubjectTypes.Organization, posting.Id.Value),
                Evidence = [.. evidenceMediaIds.Select(TrustVerificationEvidence.Media)],
                SupportingNotes = notes,
                CorrelationId = posting.Id.Value,
                RequestedBy = JobsModuleMetadata.Name,
                CountryCode = posting.Location.CountryCode,
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["jobTitle"] = posting.Title.Value,
                    ["employerId"] = posting.EmployerId.Value.ToString(),
                    ["slug"] = posting.Slug.Value
                }
            },
            cancellationToken);
    }

    private async Task<Result<Guid>> SubmitAsync(
        TrustVerificationSubmissionRequest request, CancellationToken cancellationToken)
    {
        if (_submission is null)
        {
            return Result.Failure<Guid>(JobErrors.VerificationUnavailable);
        }

        try
        {
            var result = await _submission.SubmitAsync(request, cancellationToken);

            if (result.IsFailure)
            {
                _logger.LogWarning(
                    "Trust refused a {Type} verification for subject {SubjectId}: {Error}.",
                    request.VerificationType,
                    request.Subject.SubjectId,
                    result.Error.Description);

                // Trust's own refusal reason is passed through unchanged. Replacing it with a
                // generic message would leave the claimant unable to fix what was wrong.
                return Result.Failure<Guid>(result.Error);
            }

            return Result.Success(result.Value.VerificationId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A Trust outage is unavailability, not a crash. See the summary.
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Submitting a {Type} verification to Trust failed.",
                request.VerificationType);

            return Result.Failure<Guid>(JobErrors.VerificationUnavailable);
        }
#pragma warning restore CA1031
    }
}
