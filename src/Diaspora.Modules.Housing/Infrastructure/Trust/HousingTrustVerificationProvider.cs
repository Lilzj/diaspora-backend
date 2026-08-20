using Diaspora.Contracts.Trust.V1.Submission;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Housing.Infrastructure.Trust;

/// <summary>
/// The one class in B11 that talks to Trust.
/// <para>
/// <b>It reaches Trust through the published <c>ITrustVerificationSubmission</c> contract in
/// <c>Diaspora.Contracts</c>, and holds no reference to the Trust module.</b> That is B3.0.2's
/// whole point, and an architecture test asserts it: consuming the contract must not mean
/// referencing the module that decides.
/// </para>
/// <para>
/// <b>What it may do is ask.</b> Approval, rejection, revocation, badge assignment and outcome
/// publication are Trust's alone and are not reachable from here — which is why the methods return
/// a case identifier rather than a decision, and why nothing in this file can set a
/// <c>HousingTrustMark</c> to verified.
/// </para>
/// <para>
/// <b>Why a verified landlord matters more here than a verified seller does in B10.</b> A
/// marketplace badge tells a buyer a second-hand bicycle is probably real. A housing badge tells
/// somebody who has not yet arrived in the country that the person asking for a deposit exists and
/// owns the property — which is the single claim that a rental advance-fee fraud has to defeat.
/// Getting the subject model right is therefore not bookkeeping: a business claim names the
/// landlord profile so two agencies run by one member stay distinguishable, and an individual claim
/// names the member because that is who is being identified.
/// </para>
/// <para>
/// <b>The dependency is optional by design.</b> A deployment that does not load Trust has no
/// verification engine at all, so the contract is injected nullable and a missing implementation
/// degrades to "unavailable" rather than throwing. B7, B9 and B10 do exactly the same.
/// </para>
/// </summary>
public sealed class HousingTrustVerificationProvider
{
    private readonly ITrustVerificationSubmission? _submission;
    private readonly ILogger<HousingTrustVerificationProvider> _logger;

    public HousingTrustVerificationProvider(
        ILogger<HousingTrustVerificationProvider> logger,
        ITrustVerificationSubmission? submission = null)
    {
        _submission = submission;
        _logger = logger;
    }

    /// <summary>Whether this deployment has a verification engine at all.</summary>
    public bool IsAvailable => _submission is not null;

    /// <summary>
    /// Asks Trust to verify a landlord.
    /// <para>
    /// Submitted as <c>Business</c> or <c>Identity</c> according to what the landlord declared,
    /// against the landlord profile's own identifier for a business and against the member for an
    /// individual — exactly the gap B3.0.2's subject model closed. The correlation identifier is
    /// the profile's, so a retry after a timeout returns the original case rather than opening a
    /// second.
    /// </para>
    /// <para>
    /// The registration number and legal name go in the opaque metadata bag, where Trust displays
    /// them to a reviewer and never parses them. The moment Trust interpreted a key there it would
    /// have learned what a landlord is, and the next vertical would need a Trust change.
    /// </para>
    /// </summary>
    public async Task<Result<Guid>> SubmitLandlordAsync(
        LandlordProfile landlord,
        UserId requestedBy,
        IReadOnlyList<Guid> evidenceMediaIds,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(landlord);
        ArgumentNullException.ThrowIfNull(requestedBy);
        ArgumentNullException.ThrowIfNull(evidenceMediaIds);

        var isBusiness = landlord.IsBusiness;

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["landlordName"] = landlord.Identity.VerifiableName,
            ["landlordKind"] = landlord.Identity.Kind.ToString(),
            ["slug"] = landlord.Slug.Value
        };

        if (landlord.Identity.RegistrationNumber is { Length: > 0 } registration)
        {
            metadata["registrationNumber"] = registration;
        }

        if (landlord.ProviderId is { } providerId)
        {
            metadata["providerId"] = providerId.ToString();
        }

        return await SubmitAsync(
            new TrustVerificationSubmissionRequest
            {
                VerificationType = isBusiness ? "Business" : "Identity",
                SubmittedBy = requestedBy.Value,
                Subject = TrustVerificationSubject.For(
                    isBusiness
                        ? TrustVerificationSubjectTypes.Business
                        : TrustVerificationSubjectTypes.Member,
                    isBusiness ? landlord.Id.Value : landlord.OwnerUserId.Value),
                Evidence = [.. evidenceMediaIds.Select(TrustVerificationEvidence.Media)],
                SupportingNotes = notes,
                CorrelationId = landlord.Id.Value,
                RequestedBy = HousingModuleMetadata.Name,
                CountryCode = landlord.PrimaryCountryCode,
                Metadata = metadata
            },
            cancellationToken);
    }

    /// <summary>
    /// Asks Trust to vouch for a particular property.
    /// <para>
    /// Submitted as <c>Organization</c> against the listing's identifier. A property-level claim is
    /// a narrower thing than a landlord-level one — it says "this particular home exists and is
    /// this landlord's to let", which is the claim that matters on a high-value let or one a
    /// member is taking sight-unseen from another country — and keeping the subjects distinct is
    /// what lets a verified landlord still have one property reviewed.
    /// </para>
    /// </summary>
    public async Task<Result<Guid>> SubmitListingAsync(
        HousingListing listing,
        UserId requestedBy,
        IReadOnlyList<Guid> evidenceMediaIds,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listing);
        ArgumentNullException.ThrowIfNull(requestedBy);
        ArgumentNullException.ThrowIfNull(evidenceMediaIds);

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["listingTitle"] = listing.Title.Value,
            ["landlordId"] = listing.LandlordId.Value.ToString(),
            ["accommodationType"] = listing.AccommodationTypeKey,
            ["slug"] = listing.Slug.Value
        };

        // The neighbourhood, where there is one. A reviewer checking that a property exists needs
        // to know roughly where it is, and the neighbourhood is as precise as this module will put
        // in a payload — the address is disclosed to a viewer with a confirmed viewing, not to a
        // metadata bag.
        if (listing.Location.NeighborhoodName is { Length: > 0 } neighborhood)
        {
            metadata["neighborhood"] = neighborhood;
        }

        return await SubmitAsync(
            new TrustVerificationSubmissionRequest
            {
                VerificationType = "Organization",
                SubmittedBy = requestedBy.Value,
                Subject = TrustVerificationSubject.For(
                    TrustVerificationSubjectTypes.Organization, listing.Id.Value),
                Evidence = [.. evidenceMediaIds.Select(TrustVerificationEvidence.Media)],
                SupportingNotes = notes,
                CorrelationId = listing.Id.Value,
                RequestedBy = HousingModuleMetadata.Name,
                CountryCode = listing.Location.CountryCode,
                Metadata = metadata
            },
            cancellationToken);
    }

    private async Task<Result<Guid>> SubmitAsync(
        TrustVerificationSubmissionRequest request, CancellationToken cancellationToken)
    {
        if (_submission is null)
        {
            return Result.Failure<Guid>(HousingErrors.VerificationUnavailable);
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

            return Result.Failure<Guid>(HousingErrors.VerificationUnavailable);
        }
#pragma warning restore CA1031
    }
}
