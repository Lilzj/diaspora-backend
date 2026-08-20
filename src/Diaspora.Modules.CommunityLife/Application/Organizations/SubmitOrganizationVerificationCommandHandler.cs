using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>
/// Asks Trust to verify an organization.
/// <para>
/// <b>Only an owner or administrator may ask.</b> A badge on a church, a mosque or a charity is what
/// a member relies on before handing over a collection or bringing their children somewhere, and a
/// claim anybody could file on any body's behalf would be worth nothing.
/// </para>
/// <para>
/// <b>Trust is asked before the aggregate is moved.</b> A pending trust mark with no case behind it
/// is a body that can never be verified and can never re-submit, because the aggregate refuses a
/// second submission while one is pending. Asking first means a Trust outage leaves the organization
/// exactly as it was.
/// </para>
/// <para>
/// <b>The evidence is attached to the aggregate under its own role.</b> That is what keeps a
/// constitution out of the public gallery — the mapper filters the role out, and the relay refuses
/// to announce it.
/// </para>
/// </summary>
public sealed class SubmitOrganizationVerificationCommandHandler
    : ICommandHandler<SubmitOrganizationVerificationCommand, OrganizationResponse>
{
    private readonly IOrganizationRepository _organizations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityLifeTrustProvider _trust;
    private readonly CommunityLifeMediaClaimer _media;
    private readonly OrganizationReadContext _read;
    private readonly IClock _clock;

    public SubmitOrganizationVerificationCommandHandler(
        IOrganizationRepository organizations,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        CommunityLifeTrustProvider trust,
        CommunityLifeMediaClaimer media,
        OrganizationReadContext read,
        IClock clock)
    {
        _organizations = organizations;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _trust = trust;
        _media = media;
        _read = read;
        _clock = clock;
    }

    public async Task<Result<OrganizationResponse>> Handle(
        SubmitOrganizationVerificationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var organization = await _organizations.GetAsync(
            OrganizationId.From(command.OrganizationId), cancellationToken);

        if (organization is null)
        {
            return Result.Failure<OrganizationResponse>(
                CommunityLifeErrors.OrganizationNotFound);
        }

        var caller = await _authorization.RequireRoleAsync(
            organization.Id, MembershipRole.Administrator, cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<OrganizationResponse>(caller.Error);
        }

        if (organization.TrustMark.IsPending)
        {
            return Result.Failure<OrganizationResponse>(
                CommunityLifeErrors.VerificationAlreadyPending);
        }

        var request = command.Request;
        var now = _clock.UtcNow;

        var attached = AttachEvidence(organization, request.EvidenceMediaIds, now);

        if (attached.IsFailure)
        {
            return Result.Failure<OrganizationResponse>(attached.Error);
        }

        // Asked before the aggregate is moved. See the class summary.
        var submitted = await _trust.SubmitAsync(
            organization,
            caller.Value,
            request.EvidenceMediaIds,
            request.LegalName,
            request.RegistrationNumber,
            request.Notes,
            cancellationToken);

        if (submitted.IsFailure)
        {
            return Result.Failure<OrganizationResponse>(submitted.Error);
        }

        var marked = organization.SubmitForVerification(submitted.Value, caller.Value, now);

        if (marked.IsFailure)
        {
            return Result.Failure<OrganizationResponse>(marked.Error);
        }

        _organizations.Update(organization);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await ClaimEvidenceAsync(organization, request.EvidenceMediaIds, cancellationToken);

        return Result.Success(await _read.ToResponseAsync(organization, cancellationToken));
    }

    private static Result AttachEvidence(
        Organization organization, IReadOnlyList<Guid> mediaIds, DateTimeOffset now)
    {
        foreach (var mediaId in mediaIds.Where(id => id != Guid.Empty))
        {
            // Attaching the same asset twice is a success, so a caller retrying after a timeout
            // re-sends its evidence without being refused.
            var attached = organization.AttachMedia(
                mediaId, CommunityLifeMediaRole.VerificationEvidence, caption: null, now);

            if (attached.IsFailure)
            {
                return attached;
            }
        }

        return Result.Success();
    }

    private async Task ClaimEvidenceAsync(
        Organization organization,
        IReadOnlyList<Guid> mediaIds,
        CancellationToken cancellationToken)
    {
        var evidence = mediaIds
            .Where(id => id != Guid.Empty)
            .Select(id => (MediaId: id, Role: (string?)nameof(
                CommunityLifeMediaRole.VerificationEvidence)))
            .ToList();

        if (evidence.Count > 0)
        {
            await _media.ClaimManyForOrganizationAsync(
                organization.Id.Value, evidence, cancellationToken);
        }
    }
}
