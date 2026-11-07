using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Application.Mapping;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Employers;

/// <summary>
/// Updates an employer profile, and claims any newly attached branding assets in Media.
/// <para>
/// The Media claim is not optional: B5 sweeps unclaimed uploads, so a logo whose asset was never
/// claimed disappears a day later — silently, because both the upload and the save succeeded.
/// </para>
/// </summary>
public sealed class UpdateEmployerCommandHandler
    : ICommandHandler<UpdateEmployerCommand, EmployerResponse>
{
    private readonly IEmployerProfileRepository _employers;
    private readonly IJobPostingRepository _postings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JobAuthorizationService _authorization;
    private readonly JobMediaClaimer _media;

    public UpdateEmployerCommandHandler(
        IEmployerProfileRepository employers,
        IJobPostingRepository postings,
        IUnitOfWork unitOfWork,
        JobAuthorizationService authorization,
        JobMediaClaimer media)
    {
        _employers = employers;
        _postings = postings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _media = media;
    }

    public async Task<Result<EmployerResponse>> Handle(
        UpdateEmployerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var employerId = EmployerProfileId.From(command.EmployerId);

        var employer = await _authorization.RequireEmployerAsync(
            employerId, EmployerMemberRole.Administrator, cancellationToken);

        if (employer.IsFailure)
        {
            return Result.Failure<EmployerResponse>(employer.Error);
        }

        var request = command.Request;

        var organizationType = JobWireNames.ParseOrganizationType(request.OrganizationType);

        if (organizationType is null)
        {
            return Result.Failure<EmployerResponse>(JobErrors.EmployerNameInvalid);
        }

        var identity = CompanyIdentity.Create(
            request.DisplayName,
            request.LegalName,
            request.RegistrationNumber,
            organizationType.Value,
            JobWireNames.ParseCompanySize(request.CompanySize) ?? CompanySize.Unspecified,
            request.FoundedYear);

        if (identity.IsFailure)
        {
            return Result.Failure<EmployerResponse>(identity.Error);
        }

        var contact = CompanyContact.Create(
            request.ContactEmail, request.ContactPhone, request.WebsiteUrl, request.CareersUrl);

        if (contact.IsFailure)
        {
            return Result.Failure<EmployerResponse>(contact.Error);
        }

        var branding = EmployerBranding.Create(
            request.LogoMediaId, request.CoverMediaId, request.Tagline);

        if (branding.IsFailure)
        {
            return Result.Failure<EmployerResponse>(branding.Error);
        }

        var previousLogo = employer.Value.Branding.LogoMediaId;
        var previousCover = employer.Value.Branding.CoverMediaId;

        var updated = employer.Value.UpdateProfile(
            identity.Value,
            contact.Value,
            branding.Value,
            request.Description,
            request.CountryCode,
            request.BaseRevision,
            _authorization.CallerId);

        if (updated.IsFailure)
        {
            return Result.Failure<EmployerResponse>(updated.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // After the commit, and only for assets that are actually new. Re-claiming an unchanged
        // logo on every profile save would be a Media round trip per edit for nothing.
        await ClaimNewBrandingAsync(
            employer.Value, previousLogo, previousCover, cancellationToken);

        var openJobs = await _postings.CountOpenByEmployerAsync(employerId, cancellationToken);

        // Only the employer's own administrators reach this line (staff are refused by the owner
        // check like any outsider), so the viewer's role alone decides the insider view.
        return Result.Success(EmployerMapper.ToResponse(
            employer.Value,
            openJobs,
            EmployerMapper.ViewerRole(employer.Value, _authorization.CallerId)));
    }

    private async Task ClaimNewBrandingAsync(
        EmployerProfile employer,
        Guid? previousLogo,
        Guid? previousCover,
        CancellationToken cancellationToken)
    {
        if (employer.Branding.LogoMediaId is { } logo && logo != previousLogo)
        {
            await _media.ClaimAsync(
                employer.Id.Value, logo, nameof(JobMediaRole.Logo), 0, cancellationToken);
        }

        if (employer.Branding.CoverMediaId is { } cover && cover != previousCover)
        {
            await _media.ClaimAsync(
                employer.Id.Value, cover, nameof(JobMediaRole.Cover), 0, cancellationToken);
        }
    }
}
