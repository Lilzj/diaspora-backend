using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Providers.Application.Mapping;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Contracts.V1.Requests;
using Diaspora.Modules.Providers.Contracts.V1.Responses;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Application.Management;

/// <summary>
/// <c>PATCH /providers/{id}/details</c>.
/// <para>
/// <b>Added 14 Oct 2026.</b> After registering, an owner could change a provider's name and
/// category and nothing else: not the description, not a telephone number, not where it is. A
/// business registered without a town (the app did not ask for one until that day) could never
/// be placed, and so never be found by any search narrower than its country.
/// </para>
/// </summary>
public sealed class UpdateProviderDetailsCommandHandler
    : ICommandHandler<UpdateProviderDetailsCommand, ProviderResponse>
{
    private readonly IProviderRepository _providers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ProviderAuthorizationService _authorization;
    private readonly GeographicScopeAdapter _geography;
    private readonly ProviderReadContextBuilder _contexts;
    private readonly ProviderMapper _mapper;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public UpdateProviderDetailsCommandHandler(
        IProviderRepository providers,
        IUnitOfWork unitOfWork,
        ProviderAuthorizationService authorization,
        GeographicScopeAdapter geography,
        ProviderReadContextBuilder contexts,
        ProviderMapper mapper,
        IGuidGenerator guids,
        IClock clock)
    {
        _providers = providers;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _geography = geography;
        _contexts = contexts;
        _mapper = mapper;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<ProviderResponse>> Handle(
        UpdateProviderDetailsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var provider = await _providers.GetAsync(ProviderId.From(command.ProviderId), cancellationToken);

        if (provider is null)
        {
            return Result.Failure<ProviderResponse>(ProviderErrors.NotFound);
        }

        var caller = _authorization.RequireRole(provider, ProviderRole.Editor);

        if (caller.IsFailure)
        {
            return Result.Failure<ProviderResponse>(caller.Error);
        }

        var request = command.Request;

        if (request.Description is not null)
        {
            var description = ProviderDescription.Create(request.Description, provider.Profile.Description.Summary);

            if (description.IsFailure)
            {
                return Result.Failure<ProviderResponse>(description.Error);
            }

            var profile = provider.UpdateProfile(
                description.Value,
                provider.Profile.Tagline,
                provider.Profile.Mission,
                provider.Profile.FoundedOn,
                DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime),
                caller.Value);

            if (profile.IsFailure)
            {
                return Result.Failure<ProviderResponse>(profile.Error);
            }
        }

        foreach (var (kind, value) in new (ContactMethodKind Kind, string? Value)[]
                 {
                     (ContactMethodKind.Phone, request.Phone),
                     (ContactMethodKind.WhatsApp, request.WhatsApp),
                     (ContactMethodKind.Email, request.Email),
                     (ContactMethodKind.Website, request.Website)
                 })
        {
            var replaced = ReplaceContact(provider, kind, value, caller.Value);

            if (replaced.IsFailure)
            {
                return Result.Failure<ProviderResponse>(replaced.Error);
            }
        }

        if (request.CountryCode is not null || request.CityName is not null || request.AddressLine is not null
            || request.PostalCode is not null || request.Latitude is not null || request.Longitude is not null)
        {
            var location = await ResolveLocationAsync(provider, request, caller.Value, cancellationToken);

            if (location.IsFailure)
            {
                return Result.Failure<ProviderResponse>(location.Error);
            }

            var moved = provider.SetLocation(location.Value, caller.Value);

            if (moved.IsFailure)
            {
                return Result.Failure<ProviderResponse>(moved.Error);
            }
        }

        provider.RecomputeProfileCompletion();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var context = await _contexts.BuildAsync(
            [provider], includeViewerReviews: true, cancellationToken: cancellationToken);

        return Result.Success(_mapper.ToResponse(provider, context));
    }

    /// <summary>
    /// Null leaves the kind alone; empty removes it; a value replaces every method of that kind with
    /// one primary method. A value that does not parse is refused, unlike at registration where a
    /// stray character in an optional field should not lose the whole form: here it is the one
    /// field the owner is changing, and they should be told.
    /// </summary>
    private Result ReplaceContact(
        Provider provider, ContactMethodKind kind, string? value, SharedKernel.Identity.UserId changedBy)
    {
        if (value is null)
        {
            return Result.Success();
        }

        ProviderContactMethod? replacement = null;

        if (!string.IsNullOrWhiteSpace(value))
        {
            var created = ProviderContactMethod.Create(
                ProviderContactMethodId.From(_guids.NewGuid()), kind, value.Trim(), isPrimary: true);

            if (created.IsFailure)
            {
                return created;
            }

            replacement = created.Value;
        }

        foreach (var existing in provider.ContactMethods.Where(method => method.Kind == kind).ToList())
        {
            var removed = provider.RemoveContactMethod(existing.Id, changedBy);

            if (removed.IsFailure)
            {
                return removed;
            }
        }

        return replacement is null ? Result.Success() : provider.AddContactMethod(replacement, changedBy);
    }

    /// <summary>Exactly registration's placement, starting from what the provider already has.</summary>
    private async Task<Result<ProviderLocationContext>> ResolveLocationAsync(
        Provider provider,
        UpdateProviderDetailsRequest request,
        SharedKernel.Identity.UserId ownerId,
        CancellationToken cancellationToken)
    {
        var current = provider.Location;

        // A new town or country is a new place: the old pin and street belong to the old one, and
        // the pin would win (placement reads coordinates before the town's name), leaving the
        // business where it was. They are kept only when the place itself is not changing.
        var placeChanged = request.CityName is not null || request.CountryCode is not null;

        var countryCode = request.CountryCode ?? current.CountryCode;
        var cityName = request.CityName ?? current.CityName;
        var addressLine = request.AddressLine ?? (placeChanged ? null : current.AddressLine);
        var postalCode = request.PostalCode ?? (placeChanged ? null : current.PostalCode);
        var latitude = request.Latitude ?? (placeChanged ? null : current.Latitude);
        var longitude = request.Longitude ?? (placeChanged ? null : current.Longitude);

        var scope = await _geography.ResolveAsync(
            new ProviderLocationRequest
            {
                CountryCode = countryCode,
                CityName = cityName,
                AddressLine = addressLine,
                PostalCode = postalCode,
                Latitude = latitude,
                Longitude = longitude
            },
            ownerId,
            cancellationToken);

        if (scope.IsFailure)
        {
            return Result.Failure<ProviderLocationContext>(scope.Error);
        }

        var resolved = ProviderPlacement.FromScope(
            scope.Value, cityName, latitude, longitude, addressLine, postalCode);

        // Not filed under a catalogue area (14 Oct 2026); see RegisterProviderCommandHandler.
        return Result.Success(resolved);
    }
}

/// <summary><c>DELETE /providers/{id}/photos/{mediaId}</c> (14 Oct 2026).</summary>
public sealed class RemoveProviderPhotoCommandHandler
    : ICommandHandler<RemoveProviderPhotoCommand, ProviderResponse>
{
    private readonly IProviderRepository _providers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ProviderAuthorizationService _authorization;
    private readonly ProviderReadContextBuilder _contexts;
    private readonly ProviderMapper _mapper;

    public RemoveProviderPhotoCommandHandler(
        IProviderRepository providers,
        IUnitOfWork unitOfWork,
        ProviderAuthorizationService authorization,
        ProviderReadContextBuilder contexts,
        ProviderMapper mapper)
    {
        _providers = providers;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _contexts = contexts;
        _mapper = mapper;
    }

    public async Task<Result<ProviderResponse>> Handle(
        RemoveProviderPhotoCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var provider = await _providers.GetAsync(ProviderId.From(command.ProviderId), cancellationToken);

        if (provider is null)
        {
            return Result.Failure<ProviderResponse>(ProviderErrors.NotFound);
        }

        var caller = _authorization.RequireRole(provider, ProviderRole.Editor);

        if (caller.IsFailure)
        {
            return Result.Failure<ProviderResponse>(caller.Error);
        }

        var items = provider.Gallery.Where(item => item.MediaId.Value == command.MediaId).ToList();
        var wasCover = provider.Profile.CoverMediaId?.Value == command.MediaId;

        if (items.Count == 0 && !wasCover)
        {
            return Result.Failure<ProviderResponse>(ProviderErrors.GalleryItemNotFound);
        }

        foreach (var item in items)
        {
            var removed = provider.RemoveGalleryItem(item.Id);

            if (removed.IsFailure)
            {
                return Result.Failure<ProviderResponse>(removed.Error);
            }
        }

        // The cover follows the gallery: the next photograph takes its place, or none.
        if (wasCover)
        {
            provider.SetCoverMedia(provider.Gallery.Count > 0 ? provider.Gallery[0].MediaId : null);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var context = await _contexts.BuildAsync(
            [provider], includeViewerReviews: true, cancellationToken: cancellationToken);

        return Result.Success(_mapper.ToResponse(provider, context));
    }
}
