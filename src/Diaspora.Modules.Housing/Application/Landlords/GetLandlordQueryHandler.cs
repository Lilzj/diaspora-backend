using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Landlords;

/// <summary>
/// Reads one landlord profile.
/// <para>
/// Unauthenticated, deliberately: a member deciding whether to trust a letting agency should be
/// able to look it up before they join anything, and the whole point of a verified landlord badge
/// is that somebody outside the platform can see it. What they see is redacted — contact details
/// stay hidden and the no-show count is absent — so openness costs the landlord nothing.
/// </para>
/// </summary>
public sealed class GetLandlordQueryHandler
    : IQueryHandler<GetLandlordQuery, LandlordProfileResponse>
{
    private readonly ILandlordProfileRepository _landlords;
    private readonly HousingAuthorizationService _authorization;

    public GetLandlordQueryHandler(
        ILandlordProfileRepository landlords, HousingAuthorizationService authorization)
    {
        _landlords = landlords;
        _authorization = authorization;
    }

    public async Task<Result<LandlordProfileResponse>> Handle(
        GetLandlordQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = await ResolveAsync(request.IdOrSlug, cancellationToken);

        if (profile is null)
        {
            return Result.Failure<LandlordProfileResponse>(HousingErrors.LandlordNotFound);
        }

        var isOwner = _authorization.CallerId is { } callerId
            && profile.OwnerUserId == callerId;

        return Result.Success(HousingLandlordMapper.ToResponse(
            profile,
            isOwner,
            _authorization.IsStaff,

            // A profile read is not a viewing. Contact details stay hidden unless the landlord
            // published them; a member who needs to ring somebody books a viewing first, which is
            // exactly the flow the module is built around.
            hasConfirmedViewing: false));
    }

    /// <summary>
    /// Resolves the route parameter, trying an identifier first and then a slug.
    /// <para>
    /// Identifier first because a GUID is unambiguous and a slug is not — and because a slug that
    /// happens to parse as a GUID does not exist. B4's neighbourhood hub resolves the same way.
    /// </para>
    /// </summary>
    private async Task<LandlordProfile?> ResolveAsync(
        string idOrSlug, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(idOrSlug, out var id))
        {
            var byId = await _landlords.GetForReadAsync(
                LandlordProfileId.From(id), cancellationToken);

            if (byId is not null)
            {
                return byId;
            }
        }

        var slug = HousingSlug.TryCreate(idOrSlug);

        return slug.IsFailure
            ? null
            : await _landlords.GetBySlugAsync(slug.Value, cancellationToken);
    }
}
