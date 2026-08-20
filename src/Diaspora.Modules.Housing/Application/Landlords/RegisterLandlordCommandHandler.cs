using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Landlords;

/// <summary>
/// Sets up a landlord profile.
/// <para>
/// <b>One per member, checked against soft-deleted rows too.</b> A member whose profile was closed
/// must not be able to open a second one, or two profiles would claim one person's listings and no
/// read path could say which was right.
/// </para>
/// <para>
/// <b>Linking a B7 provider is checked, not trusted.</b> A member who could name any provider
/// identifier could attach their listings to somebody else's agency and inherit its reputation.
/// The resolver asks Providers whether the provider exists and belongs to this member.
/// </para>
/// </summary>
public sealed class RegisterLandlordCommandHandler
    : ICommandHandler<RegisterLandlordCommand, LandlordProfileResponse>
{
    private readonly ILandlordProfileRepository _landlords;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingAuthorizationService _authorization;
    private readonly HousingSlugAllocator _slugs;
    private readonly HousingNeighborhoodResolver _geography;
    private readonly HousingProviderResolver _providers;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public RegisterLandlordCommandHandler(
        ILandlordProfileRepository landlords,
        IUnitOfWork unitOfWork,
        HousingAuthorizationService authorization,
        HousingSlugAllocator slugs,
        HousingNeighborhoodResolver geography,
        HousingProviderResolver providers,
        IGuidGenerator guids,
        IClock clock)
    {
        _landlords = landlords;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _slugs = slugs;
        _geography = geography;
        _providers = providers;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<LandlordProfileResponse>> Handle(
        RegisterLandlordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<LandlordProfileResponse>(caller.Error);
        }

        if (await _landlords.ExistsForUserAsync(caller.Value, cancellationToken))
        {
            return Result.Failure<LandlordProfileResponse>(
                HousingErrors.LandlordAlreadyExists);
        }

        var request = command.Request;

        if (!Enum.TryParse<LandlordKind>(request.Kind, ignoreCase: true, out var kind))
        {
            return Result.Failure<LandlordProfileResponse>(HousingErrors.LandlordKindInvalid);
        }

        if (!await _geography.CountryExistsAsync(request.CountryCode, cancellationToken))
        {
            return Result.Failure<LandlordProfileResponse>(HousingErrors.CountryInvalid);
        }

        var identity = LandlordIdentity.Create(
            request.DisplayName, kind, request.LegalName, request.RegistrationNumber);

        if (identity.IsFailure)
        {
            return Result.Failure<LandlordProfileResponse>(identity.Error);
        }

        var contact = LandlordContact.Create(
            request.PhoneNumber,
            request.Email,
            request.WebsiteUrl,
            request.ShowContactPublicly);

        if (contact.IsFailure)
        {
            return Result.Failure<LandlordProfileResponse>(contact.Error);
        }

        var slug = await _slugs.ForLandlordAsync(request.DisplayName, cancellationToken);

        var profile = LandlordProfile.Register(
            LandlordProfileId.From(_guids.NewSequentialGuid()),
            caller.Value,
            identity.Value,
            slug,
            request.CountryCode,
            contact.Value,
            _clock.UtcNow);

        if (profile.IsFailure)
        {
            return Result.Failure<LandlordProfileResponse>(profile.Error);
        }

        if (request.ProviderId is { } providerId && providerId != Guid.Empty)
        {
            // Checked rather than trusted. See the class summary.
            var linked = await _providers.LinkAsync(
                profile.Value, providerId, caller.Value, cancellationToken);

            if (linked.IsFailure)
            {
                return Result.Failure<LandlordProfileResponse>(linked.Error);
            }
        }

        if (request.LogoMediaId is { } logoId && logoId != Guid.Empty)
        {
            profile.Value.SetLogo(logoId);
        }

        if (!string.IsNullOrWhiteSpace(request.Bio))
        {
            profile.Value.Update(baseRevision: null, bio: request.Bio);
        }

        await _landlords.AddAsync(profile.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(HousingLandlordMapper.ToResponse(
            profile.Value, isOwner: true, isStaff: false, hasConfirmedViewing: true));
    }
}
