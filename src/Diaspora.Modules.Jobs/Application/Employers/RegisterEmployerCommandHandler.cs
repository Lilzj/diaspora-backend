using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Application.Mapping;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Employers;

/// <summary>
/// Creates an employer profile.
/// <para>
/// The owner membership is created inside the aggregate rather than here, because an employer
/// with no owner is one nobody can ever administer — and that invariant has to hold from the
/// first instant, not from the first successful follow-up write.
/// </para>
/// </summary>
public sealed class RegisterEmployerCommandHandler
    : ICommandHandler<RegisterEmployerCommand, EmployerResponse>
{
    private readonly IEmployerProfileRepository _employers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JobAuthorizationService _authorization;
    private readonly JobSlugAllocator _slugs;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public RegisterEmployerCommandHandler(
        IEmployerProfileRepository employers,
        IUnitOfWork unitOfWork,
        JobAuthorizationService authorization,
        JobSlugAllocator slugs,
        IGuidGenerator guids,
        IClock clock)
    {
        _employers = employers;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _slugs = slugs;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<EmployerResponse>> Handle(
        RegisterEmployerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<EmployerResponse>(caller.Error);
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

        var slug = await _slugs.AllocateEmployerSlugAsync(
            request.Slug, request.DisplayName, cancellationToken);

        if (slug.IsFailure)
        {
            return Result.Failure<EmployerResponse>(slug.Error);
        }

        var employer = EmployerProfile.Create(
            EmployerProfileId.From(_guids.NewSequentialGuid()),
            caller.Value,
            identity.Value,
            slug.Value,
            request.CountryCode,
            _clock.UtcNow);

        if (employer.IsFailure)
        {
            return Result.Failure<EmployerResponse>(employer.Error);
        }

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            var described = employer.Value.UpdateProfile(
                identity.Value,
                CompanyContact.Empty,
                EmployerBranding.Empty,
                request.Description,
                request.CountryCode,
                baseRevision: null,
                caller.Value);

            if (described.IsFailure)
            {
                return Result.Failure<EmployerResponse>(described.Error);
            }
        }

        await _employers.AddAsync(employer.Value, cancellationToken);

        // The transaction behaviour opens and commits; it does not save. A handler that forgets
        // this returns a 201 describing an employer that was never written.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(EmployerMapper.ToResponse(
            employer.Value, openJobCount: 0, EmployerMemberRole.Owner));
    }
}
