using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Modules.Housing.Options;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// Records what an existing household is looking for in a flatmate.
/// <para>
/// <b>The nationality preference is dropped rather than refused where the deployment has not
/// enabled it.</b> A landlord should not get a validation error for a field their client showed
/// them, and the operator decision is not theirs to argue with. The gender-preference rule is the
/// opposite: it is refused loudly on a whole property, because that is a discriminatory advert and
/// silently dropping it would leave the landlord believing it had been applied.
/// </para>
/// </summary>
public sealed class SetRoommatePreferencesCommandHandler
    : ICommandHandler<SetRoommatePreferencesCommand, HousingListingResponse>
{
    private readonly HousingListingWriteGate _gate;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingListingReadContext _read;
    private readonly IOptions<HousingOptions> _options;

    public SetRoommatePreferencesCommandHandler(
        HousingListingWriteGate gate,
        IUnitOfWork unitOfWork,
        HousingListingReadContext read,
        IOptions<HousingOptions> options)
    {
        _gate = gate;
        _unitOfWork = unitOfWork;
        _read = read;
        _options = options;
    }

    public async Task<Result<HousingListingResponse>> Handle(
        SetRoommatePreferencesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var listing = await _gate.LoadAsync(command.ListingId, cancellationToken);

        if (listing.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(listing.Error);
        }

        var request = command.Request;

        var preferences = RoommatePreferences.Create(
            Parse<GenderPreference>(request.GenderPreference) ?? GenderPreference.Any,
            Parse<RoommateOccupation>(request.Occupation) ?? RoommateOccupation.Any,
            request.MinimumAge,
            request.MaximumAge,
            Parse<SmokingPolicy>(request.SmokingPolicy) ?? SmokingPolicy.Unspecified,
            Parse<PetPolicy>(request.PetPolicy) ?? PetPolicy.Unspecified,
            Parse<NoiseTolerance>(request.NoiseTolerance) ?? NoiseTolerance.Unspecified,
            request.NationalityPreference,
            request.Languages,
            _options.Value.AllowNationalityPreference);

        if (preferences.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(preferences.Error);
        }

        var applied = listing.Value.SetRoommatePreferences(
            preferences.Value, request.BaseRevision);

        if (applied.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(applied.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await _read.ToResponseAsync(listing.Value, cancellationToken);

        return response is null
            ? Result.Failure<HousingListingResponse>(HousingErrors.ListingNotFound)
            : Result.Success(response);
    }

    private static TEnum? Parse<TEnum>(string? value) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : null;
}
