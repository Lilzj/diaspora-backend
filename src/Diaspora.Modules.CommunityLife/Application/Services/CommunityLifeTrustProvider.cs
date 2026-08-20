using Diaspora.Contracts.Trust.V1.Submission;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.CommunityLife.Application.Services;

/// <summary>
/// The one class in B12 that talks to Trust.
/// <para>
/// <b>It reaches Trust through the published <c>ITrustVerificationSubmission</c> contract in
/// <c>Diaspora.Contracts</c>, and holds no reference to the Trust module.</b> That is B3.0.2's whole
/// point, and an architecture test asserts it.
/// </para>
/// <para>
/// <b>What it may do is ask.</b> Approval, rejection, revocation, badge assignment and outcome
/// publication are Trust's alone — which is why this returns a case identifier rather than a
/// decision, and why nothing in this file can set a <c>CommunityLifeTrustMark</c> to verified.
/// </para>
/// <para>
/// <b>Why a verified organization matters.</b> A badge on a church, a mosque or a charity is the
/// claim a member relies on before handing over a collection or bringing their children somewhere.
/// The subject is the organization rather than the member who created it, so two bodies run by one
/// person stay distinguishable — exactly the gap B3.0.2's subject model closed.
/// </para>
/// <para>
/// <b>The dependency is optional by design.</b> A deployment that does not load Trust has no
/// verification engine at all, so the contract is injected nullable and a missing implementation
/// degrades to "unavailable" rather than throwing. B7, B9, B10 and B11 do exactly the same.
/// </para>
/// </summary>
public sealed class CommunityLifeTrustProvider
{
    private readonly ITrustVerificationSubmission? _submission;
    private readonly ILogger<CommunityLifeTrustProvider> _logger;

    public CommunityLifeTrustProvider(
        ILogger<CommunityLifeTrustProvider> logger,
        ITrustVerificationSubmission? submission = null)
    {
        _submission = submission;
        _logger = logger;
    }

    /// <summary>Whether this deployment has a verification engine at all.</summary>
    public bool IsAvailable => _submission is not null;

    /// <summary>
    /// Asks Trust to verify an organization.
    /// <para>
    /// Submitted as a business claim against the organization's own identifier, with the legal name
    /// and registration number in the opaque metadata bag where Trust displays them to a reviewer
    /// and never parses them. The moment Trust interpreted a key there it would have learned what an
    /// organization is, and the next vertical would need a Trust change.
    /// </para>
    /// <para>
    /// The correlation identifier is the organization's, so a retry after a timeout returns the
    /// original case rather than opening a second.
    /// </para>
    /// </summary>
    public async Task<Result<Guid>> SubmitAsync(
        Organization organization,
        UserId requestedBy,
        IReadOnlyList<Guid> evidenceMediaIds,
        string? legalName,
        string? registrationNumber,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organization);
        ArgumentNullException.ThrowIfNull(requestedBy);
        ArgumentNullException.ThrowIfNull(evidenceMediaIds);

        if (_submission is null)
        {
            _logger.LogWarning(
                "Verification requested for organization {OrganizationId} but no Trust engine is loaded.",
                organization.Id.Value);

            return Result.Failure<Guid>(CommunityLifeErrors.VerificationUnavailable);
        }

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["organizationTypeKey"] = organization.OrganizationTypeKey,
            ["countryCode"] = organization.Location.CountryCode,
            ["memberCount"] = organization.Statistics.MemberCount.ToString(
                System.Globalization.CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrWhiteSpace(legalName))
        {
            metadata["legalName"] = legalName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(registrationNumber))
        {
            metadata["registrationNumber"] = registrationNumber.Trim();
        }

        metadata["organizationName"] = organization.Name.Value;
        metadata["slug"] = organization.Slug.Value;

        var request = new TrustVerificationSubmissionRequest
        {
            VerificationType = "Organization",
            SubmittedBy = requestedBy.Value,
            Subject = TrustVerificationSubject.For(
                TrustVerificationSubjectTypes.Organization, organization.Id.Value),

            // Untyped evidence, deliberately. Trust refuses a submission whose evidence carries a
            // document type the verification type does not accept, and this module has no business
            // knowing Trust's document vocabulary — B3.0.2 records the trap. An untyped reference is
            // accepted wherever <c>Other</c> is.
            Evidence =
            [
                .. evidenceMediaIds
                    .Where(id => id != Guid.Empty)
                    .Select(TrustVerificationEvidence.Media)
            ],
            SupportingNotes = notes,

            // The organization's own identifier, so a retry after a timeout returns the original
            // case rather than opening a second.
            CorrelationId = organization.Id.Value,
            RequestedBy = CommunityLifeModuleMetadata.Name,
            CountryCode = organization.Location.CountryCode,
            Metadata = metadata
        };

        try
        {
            var result = await _submission.SubmitAsync(request, cancellationToken);

            if (result.IsFailure)
            {
                _logger.LogWarning(
                    "Trust refused a verification claim for organization {OrganizationId}: {Error}.",
                    organization.Id.Value,
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
#pragma warning disable CA1031 // A Trust outage must not throw out of a publish path.
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Submitting organization {OrganizationId} for verification failed.",
                organization.Id.Value);

            return Result.Failure<Guid>(CommunityLifeErrors.VerificationUnavailable);
        }
#pragma warning restore CA1031
    }
}
