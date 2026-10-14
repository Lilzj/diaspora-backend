using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Providers.Application.Mapping;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Contracts.V1.Requests;
using Diaspora.Modules.Providers.Contracts.V1.Responses;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Policies;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Domain.ValueObjects;
using Diaspora.Modules.Providers.Extensibility.Types;
using Diaspora.Modules.Providers.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Application.Registration;

/// <summary>
/// Registers a provider.
/// <para>
/// The longest handler in the module, and every step is load-bearing: resolve the caller, check
/// the per-owner ceiling, validate the type against what this deployment offers, resolve the
/// category through the catalogue (aliases included), allocate a free slug, resolve the
/// geography through B4, build the aggregate, attach the contact methods that make it
/// publishable, and map back.
/// </para>
/// <para>
/// <b>It creates a draft.</b> Publishing is a separate, explicit act. A registration flow that
/// published immediately would put half-described businesses into the directory and give their
/// owners no chance to add a photograph before their first impression is made.
/// </para>
/// </summary>
public sealed class RegisterProviderCommandHandler
    : ICommandHandler<RegisterProviderCommand, ProviderResponse>
{
    private readonly ProviderAreaFiling _filing;
    private readonly IProviderRepository _providers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ServiceCategoryResolver _categories;
    private readonly ProviderSlugAllocator _slugs;
    private readonly GeographicScopeAdapter _geography;
    private readonly ProviderTypeRegistry _types;
    private readonly ProviderAuthorizationService _authorization;
    private readonly ProviderReadContextBuilder _contexts;
    private readonly ProviderMapper _mapper;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public RegisterProviderCommandHandler(
        ProviderAreaFiling filing,
        IProviderRepository providers,
        IUnitOfWork unitOfWork,
        ServiceCategoryResolver categories,
        ProviderSlugAllocator slugs,
        GeographicScopeAdapter geography,
        ProviderTypeRegistry types,
        ProviderAuthorizationService authorization,
        ProviderReadContextBuilder contexts,
        ProviderMapper mapper,
        IGuidGenerator guids,
        IClock clock)
    {
        _filing = filing;
        _providers = providers;
        _unitOfWork = unitOfWork;
        _categories = categories;
        _slugs = slugs;
        _geography = geography;
        _types = types;
        _authorization = authorization;
        _contexts = contexts;
        _mapper = mapper;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<ProviderResponse>> Handle(
        RegisterProviderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<ProviderResponse>(caller.Error);
        }

        var request = command.Request;

        var providerType = ProviderWireNames.ParseProviderType(request.ProviderType);

        if (providerType.IsFailure)
        {
            return Result.Failure<ProviderResponse>(providerType.Error);
        }

        // The deployment decides which kinds it accepts. A country pilot without embassy
        // relationships turns GovernmentService off, and this is where that lands.
        if (!_types.IsAvailable(providerType.Value))
        {
            return Result.Failure<ProviderResponse>(ProviderErrors.TypeUnsupported);
        }

        var owned = await _providers.CountOwnedByAsync(caller.Value, cancellationToken);

        if (owned >= ProviderLimits.MaxProvidersPerOwner)
        {
            return Result.Failure<ProviderResponse>(ProviderErrors.LimitReached);
        }

        var name = ProviderName.Create(request.Name);

        if (name.IsFailure)
        {
            return Result.Failure<ProviderResponse>(name.Error);
        }

        var category = await _categories.ResolveForWriteAsync(request.Category, cancellationToken);

        if (category.IsFailure)
        {
            return Result.Failure<ProviderResponse>(category.Error);
        }

        var slug = await _slugs.AllocateAsync(request.Slug, name.Value, cancellationToken);

        if (slug.IsFailure)
        {
            return Result.Failure<ProviderResponse>(slug.Error);
        }

        var location = await ResolveLocationAsync(request, caller.Value, cancellationToken);

        if (location.IsFailure)
        {
            return Result.Failure<ProviderResponse>(location.Error);
        }

        var description = ProviderDescription.Create(request.Description);

        if (description.IsFailure)
        {
            return Result.Failure<ProviderResponse>(description.Error);
        }

        var descriptor = _types.Find(providerType.Value);

        var deliveryMode = ParseDeliveryMode(request.DeliveryMode)
            ?? descriptor?.DefaultDeliveryMode
            ?? ServiceDeliveryMode.OnSite;

        var provider = Provider.Register(
            ProviderId.From(_guids.NewSequentialGuid()),
            name.Value,
            slug.Value,
            providerType.Value,
            category.Value.Id,
            caller.Value,
            ProviderProfileId.From(_guids.NewGuid()),
            ProviderMembershipId.From(_guids.NewGuid()),
            _clock.UtcNow,
            location.Value,
            deliveryMode,
            description.Value);

        if (provider.IsFailure)
        {
            return Result.Failure<ProviderResponse>(provider.Error);
        }

        var applied = ApplyRegistrationExtras(provider.Value, request);

        if (applied.IsFailure)
        {
            return Result.Failure<ProviderResponse>(applied.Error);
        }

        await _providers.AddAsync(provider.Value, cancellationToken);

        // The transaction behaviour opens and commits a transaction; it does not save. A handler
        // that forgets this returns a 201 describing a provider that was never written, which is
        // the most confusing possible failure — every response looks right and nothing persists.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        ProviderTelemetry.ProvidersRegistered.Add(
            1, new KeyValuePair<string, object?>("type", providerType.Value.ToString()));

        var context = await _contexts.BuildAsync(
            [provider.Value], cancellationToken: cancellationToken);

        return Result.Success(_mapper.ToResponse(provider.Value, context));
    }

    /// <summary>
    /// Tagline, tags and contact methods — everything the registration form collects beyond the
    /// aggregate's own construction parameters.
    /// <para>
    /// A contact method that fails to parse is <em>skipped</em> rather than failing the whole
    /// registration. Somebody who mistypes a website should still end up with a provider; the
    /// alternative loses the entire form to a stray character in an optional field.
    /// </para>
    /// </summary>
    private Result ApplyRegistrationExtras(Provider provider, RegisterProviderRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Tagline))
        {
            var profile = provider.UpdateProfile(
                provider.Profile.Description,
                request.Tagline,
                null,
                null,
                DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime));

            if (profile.IsFailure)
            {
                return profile;
            }
        }

        if (request.Tags.Count > 0)
        {
            var tags = provider.ReplaceTags(
                request.Tags.Select(tag => (ProviderTagId.From(_guids.NewGuid()), tag)));

            if (tags.IsFailure)
            {
                return tags;
            }
        }

        AddContact(provider, ContactMethodKind.Phone, request.Phone, isPrimary: true);
        AddContact(provider, ContactMethodKind.WhatsApp, request.WhatsApp, isPrimary: true);
        AddContact(provider, ContactMethodKind.Email, request.Email, isPrimary: true);
        AddContact(provider, ContactMethodKind.Website, request.Website, isPrimary: true);

        provider.RecomputeProfileCompletion();

        return Result.Success();
    }

    private void AddContact(
        Provider provider, ContactMethodKind kind, string? value, bool isPrimary)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var method = ProviderContactMethod.Create(
            ProviderContactMethodId.From(_guids.NewGuid()), kind, value, isPrimary: isPrimary);

        if (method.IsSuccess)
        {
            provider.AddContactMethod(method.Value);
        }
    }

    private async Task<Result<ProviderLocationContext>> ResolveLocationAsync(
        RegisterProviderRequest request, SharedKernel.Identity.UserId ownerId, CancellationToken cancellationToken)
    {
        var scope = await _geography.ResolveAsync(
            new ProviderLocationRequest
            {
                CountryCode = request.CountryCode,
                AreaId = request.AreaId,
                CityName = request.CityName,
                AddressLine = request.AddressLine,
                PostalCode = request.PostalCode,
                Latitude = request.Latitude,
                Longitude = request.Longitude
            },
            ownerId,
            cancellationToken);

        if (scope.IsFailure)
        {
            return Result.Failure<ProviderLocationContext>(scope.Error);
        }

        var resolved = ProviderPlacement.FromScope(
            scope.Value, request.CityName, request.Latitude, request.Longitude, request.AddressLine, request.PostalCode);

        // A scope that resolved no path — a town the search resolver could not match — used to
        // leave the provider filed nowhere, and uncounted on the map. The catalogue is asked
        // directly, from the provider's own coordinates and then the town it named.
        return Result.Success(await _filing.FileAsync(
            resolved, request.CityName, cancellationToken));
    }

    private static ServiceDeliveryMode? ParseDeliveryMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parsed = ProviderWireNames.ParseDeliveryMode(value);

        return parsed.IsSuccess ? parsed.Value : null;
    }
}
