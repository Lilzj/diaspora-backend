using Diaspora.Contracts.Trust.V1.Submission;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Marketplace.Infrastructure.Trust;

/// <summary>
/// The one class in B10 that talks to Trust.
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
/// <c>ListingTrustMark</c> to verified.
/// </para>
/// <para>
/// <b>The dependency is optional by design.</b> A deployment that does not load Trust has no
/// verification engine at all, so the contract is injected nullable and a missing implementation
/// degrades to "unavailable" rather than throwing. B7 and B9 do exactly the same, and it is the
/// difference between a module that cannot be verified and a platform that cannot start.
/// </para>
/// </summary>
public sealed class MarketplaceTrustVerificationProvider
{
    private readonly ITrustVerificationSubmission? _submission;
    private readonly ILogger<MarketplaceTrustVerificationProvider> _logger;

    public MarketplaceTrustVerificationProvider(
        ILogger<MarketplaceTrustVerificationProvider> logger,
        ITrustVerificationSubmission? submission = null)
    {
        _submission = submission;
        _logger = logger;
    }

    /// <summary>Whether this deployment has a verification engine at all.</summary>
    public bool IsAvailable => _submission is not null;

    /// <summary>
    /// Asks Trust to verify a seller.
    /// <para>
    /// Submitted as <c>Business</c> or <c>Identity</c> according to what the seller declared,
    /// against the seller profile's own identifier — which is exactly the gap B3.0.2's subject
    /// model closed. The correlation identifier is the profile's, so a retry after a timeout
    /// returns the original case rather than opening a second.
    /// </para>
    /// <para>
    /// The registration number and legal name go in the opaque metadata bag, where Trust displays
    /// them to a reviewer and never parses them. The moment Trust interpreted a key there it would
    /// have learned what a marketplace seller is, and the next vertical would need a Trust change.
    /// </para>
    /// </summary>
    public async Task<Result<Guid>> SubmitSellerAsync(
        SellerProfile seller,
        UserId requestedBy,
        IReadOnlyList<Guid> evidenceMediaIds,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seller);
        ArgumentNullException.ThrowIfNull(requestedBy);
        ArgumentNullException.ThrowIfNull(evidenceMediaIds);

        var isBusiness = seller.Identity.Kind == Domain.Enums.SellerKind.Business;

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["sellerName"] = seller.Identity.VerifiableName,
            ["sellerKind"] = seller.Identity.Kind.ToString(),
            ["slug"] = seller.Slug.Value
        };

        if (seller.Identity.RegistrationNumber is { Length: > 0 } registration)
        {
            metadata["registrationNumber"] = registration;
        }

        if (seller.ProviderId is { } providerId)
        {
            metadata["providerId"] = providerId.ToString();
        }

        return await SubmitAsync(
            new TrustVerificationSubmissionRequest
            {
                VerificationType = isBusiness ? "Business" : "Identity",
                SubmittedBy = requestedBy.Value,
                // A business claim names the seller profile, because two shops owned by one member
                // must be distinguishable — the gap B3.0.2's subject model closed. An individual
                // claim names the member, because that is who is being identified.
                Subject = TrustVerificationSubject.For(
                    isBusiness
                        ? TrustVerificationSubjectTypes.Business
                        : TrustVerificationSubjectTypes.Member,
                    isBusiness ? seller.Id.Value : seller.OwnerUserId.Value),
                Evidence = [.. evidenceMediaIds.Select(TrustVerificationEvidence.Media)],
                SupportingNotes = notes,
                CorrelationId = seller.Id.Value,
                RequestedBy = MarketplaceModuleMetadata.Name,
                CountryCode = seller.PrimaryCountryCode,
                Metadata = metadata
            },
            cancellationToken);
    }

    /// <summary>
    /// Asks Trust to vouch for a particular listing.
    /// <para>
    /// Submitted as <c>Organization</c> against the listing's identifier. A listing-level claim is
    /// a narrower thing than a seller-level one — it says "this particular item is what it appears
    /// to be", which is the claim that matters on a high-value or counterfeit-prone item — and
    /// keeping the two subjects distinct is what lets a verified seller still have one listing
    /// reviewed.
    /// </para>
    /// </summary>
    public async Task<Result<Guid>> SubmitListingAsync(
        MarketplaceListing listing,
        UserId requestedBy,
        IReadOnlyList<Guid> evidenceMediaIds,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listing);
        ArgumentNullException.ThrowIfNull(requestedBy);
        ArgumentNullException.ThrowIfNull(evidenceMediaIds);

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
                RequestedBy = MarketplaceModuleMetadata.Name,
                CountryCode = listing.Location.CountryCode,
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["listingTitle"] = listing.Title.Value,
                    ["sellerId"] = listing.SellerId.Value.ToString(),
                    ["categoryKey"] = listing.CategoryKey,
                    ["slug"] = listing.Slug.Value
                }
            },
            cancellationToken);
    }

    private async Task<Result<Guid>> SubmitAsync(
        TrustVerificationSubmissionRequest request, CancellationToken cancellationToken)
    {
        if (_submission is null)
        {
            return Result.Failure<Guid>(MarketplaceErrors.VerificationUnavailable);
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

            return Result.Failure<Guid>(MarketplaceErrors.VerificationUnavailable);
        }
#pragma warning restore CA1031
    }
}
