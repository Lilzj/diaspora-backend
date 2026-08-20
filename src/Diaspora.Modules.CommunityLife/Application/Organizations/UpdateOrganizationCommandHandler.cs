using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>
/// Edits an organization page.
/// <para>
/// <b>Null means "leave it alone" throughout.</b> A client editing the opening hours must not have
/// to round-trip the mission statement, and must not be able to blank it by omitting it — which is
/// the failure mode of a naive replace and the reason every field below is checked for null before
/// it is passed down.
/// </para>
/// <para>
/// <b>The type and the location are changed through their own aggregate methods.</b> Both are
/// resolved against a catalogue that can refuse them, and folding them into the content update
/// would mean a rejected neighbourhood also lost the caller's edit to the summary.
/// </para>
/// </summary>
public sealed class UpdateOrganizationCommandHandler
    : ICommandHandler<UpdateOrganizationCommand, OrganizationResponse>
{
    private readonly IOrganizationRepository _organizations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityLifeCatalogResolver _catalog;
    private readonly CommunityLifeNeighborhoodResolver _neighborhoods;
    private readonly CommunityLifeProviderResolver _providers;
    private readonly OrganizationReadContext _read;

    public UpdateOrganizationCommandHandler(
        IOrganizationRepository organizations,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        CommunityLifeCatalogResolver catalog,
        CommunityLifeNeighborhoodResolver neighborhoods,
        CommunityLifeProviderResolver providers,
        OrganizationReadContext read)
    {
        _organizations = organizations;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _catalog = catalog;
        _neighborhoods = neighborhoods;
        _providers = providers;
        _read = read;
    }

    public async Task<Result<OrganizationResponse>> Handle(
        UpdateOrganizationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var organization = await _organizations.GetAsync(
            OrganizationId.From(command.OrganizationId), cancellationToken);

        if (organization is null)
        {
            return Result.Failure<OrganizationResponse>(
                CommunityLifeErrors.OrganizationNotFound);
        }

        var caller = await _authorization.RequireOrganizationWriteAsync(
            organization, cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<OrganizationResponse>(caller.Error);
        }

        var request = command.Request;

        var applied = await ApplyAsync(organization, request, caller.Value, cancellationToken);

        if (applied.IsFailure)
        {
            return Result.Failure<OrganizationResponse>(applied.Error);
        }

        _organizations.Update(organization);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await _read.ToResponseAsync(organization, cancellationToken));
    }

    private async Task<Result> ApplyAsync(
        Organization organization,
        UpdateOrganizationRequest request,
        UserId caller,
        CancellationToken cancellationToken)
    {
        var content = ApplyContent(organization, request, caller);

        if (content.IsFailure)
        {
            return content;
        }

        var type = await ApplyTypeAsync(organization, request, cancellationToken);

        if (type.IsFailure)
        {
            return type;
        }

        var location = await ApplyLocationAsync(organization, request, cancellationToken);

        if (location.IsFailure)
        {
            return location;
        }

        var provider = await ApplyProviderAsync(organization, request, caller, cancellationToken);

        if (provider.IsFailure)
        {
            return provider;
        }

        return ApplyCollections(organization, request);
    }

    /// <summary>
    /// The descriptive fields, each rebuilt only when the caller sent something for it.
    /// <para>
    /// The contact details and the social links are rebuilt whole rather than merged, because the
    /// value objects validate as a unit — a caller sending a new phone number and nothing else is
    /// saying the other channels are gone, which is how a body that has stopped answering an old
    /// email removes it.
    /// </para>
    /// </summary>
    private static Result ApplyContent(
        Organization organization, UpdateOrganizationRequest request, UserId caller)
    {
        OrganizationName? name = null;

        if (request.Name is not null || request.ShortName is not null)
        {
            var built = OrganizationName.Create(
                request.Name ?? organization.Name.Value,
                request.ShortName ?? organization.Name.ShortName);

            if (built.IsFailure)
            {
                return Result.Failure(built.Error);
            }

            name = built.Value;
        }

        CommunityLifeDescription? description = null;

        if (request.Summary is not null || request.Description is not null)
        {
            var built = CommunityLifeDescription.Create(
                request.Summary ?? organization.Description.Summary,
                request.Description ?? organization.Description.Body);

            if (built.IsFailure)
            {
                return Result.Failure(built.Error);
            }

            description = built.Value;
        }

        ContactDetails? contact = null;

        if (request.Contact is { } contactRequest)
        {
            var built = ContactDetails.Create(
                contactRequest.Phone,
                contactRequest.WhatsApp,
                contactRequest.Email,
                contactRequest.Website,
                contactRequest.ContactName);

            if (built.IsFailure)
            {
                return Result.Failure(built.Error);
            }

            contact = built.Value;
        }

        SocialLinks? social = null;

        if (request.SocialLinks is { } socialRequest)
        {
            var built = SocialLinks.Create(
                socialRequest.Facebook,
                socialRequest.Instagram,
                socialRequest.Twitter,
                socialRequest.YouTube,
                socialRequest.LinkedIn,
                socialRequest.TikTok);

            if (built.IsFailure)
            {
                return Result.Failure(built.Error);
            }

            social = built.Value;
        }

        var policy = BuildPolicy(organization, request);

        if (policy.IsFailure)
        {
            return Result.Failure(policy.Error);
        }

        var updated = organization.UpdateContent(
            request.BaseRevision,
            caller,
            name,
            description,
            request.Mission,
            request.Vision,
            contact,
            social,
            policy.Value);

        if (updated.IsFailure)
        {
            return Result.Failure(updated.Error);
        }

        return ApplyVisibility(organization, request);
    }

    /// <summary>
    /// The membership policy, merged rather than replaced.
    /// <para>
    /// Unlike the contact details this one is merged field by field: a body changing only its
    /// renewal period is not saying it has stopped requiring approval, and reading it as a whole
    /// replacement would silently open a closed membership.
    /// </para>
    /// </summary>
    private static Result<MembershipPolicy?> BuildPolicy(
        Organization organization, UpdateOrganizationRequest request)
    {
        if (request.MembershipPolicy is null
            && request.MembershipRequirements is null
            && request.MembershipRenewalMonths is null
            && request.IsMembershipPublic is null)
        {
            return Result.Success<MembershipPolicy?>(null);
        }

        var kind = CommunityLifeSharedMapper.ParseEnum<MembershipPolicyKind>(
            request.MembershipPolicy) ?? organization.MembershipPolicy.Kind;

        var built = MembershipPolicy.Create(
            kind,
            request.MembershipRequirements ?? organization.MembershipPolicy.Requirements,
            request.MembershipRenewalMonths ?? organization.MembershipPolicy.RenewalMonths,
            request.IsMembershipPublic ?? organization.MembershipPolicy.IsMembershipPublic);

        return built.IsFailure
            ? Result.Failure<MembershipPolicy?>(built.Error)
            : Result.Success<MembershipPolicy?>(built.Value);
    }

    private static Result ApplyVisibility(
        Organization organization, UpdateOrganizationRequest request)
    {
        if (CommunityLifeSharedMapper.ParseEnum<OrganizationVisibility>(request.Visibility)
            is not { } visibility)
        {
            return Result.Success();
        }

        return organization.SetVisibility(visibility, request.BaseRevision);
    }

    private async Task<Result> ApplyTypeAsync(
        Organization organization,
        UpdateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.OrganizationTypeKey is null)
        {
            return Result.Success();
        }

        var type = await _catalog.RequireTypeAsync(
            request.OrganizationTypeKey, cancellationToken: cancellationToken);

        return type.IsFailure
            ? Result.Failure(type.Error)
            : organization.ChangeType(type.Value.Key, request.BaseRevision);
    }

    /// <summary>
    /// Moves the body in B4's hierarchy.
    /// <para>
    /// The unchanged parts of the address come off the aggregate, so a caller correcting a city
    /// does not have to resend a country — and cannot blank one by omitting it.
    /// </para>
    /// </summary>
    private async Task<Result> ApplyLocationAsync(
        Organization organization,
        UpdateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CountryCode is null
            && request.RegionName is null
            && request.CityName is null
            && request.NeighborhoodAreaId is null)
        {
            return Result.Success();
        }

        var context = CommunityLifeLocationContext.Create(
            request.CountryCode ?? organization.Location.CountryCode,
            request.RegionName ?? organization.Location.RegionName,
            request.CityName ?? organization.Location.CityName);

        if (context.IsFailure)
        {
            return Result.Failure(context.Error);
        }

        var resolved = await _neighborhoods.ResolveAsync(
            context.Value,
            request.NeighborhoodAreaId ?? organization.Location.NeighborhoodAreaId,
            cancellationToken);

        return resolved.IsFailure
            ? Result.Failure(resolved.Error)
            : organization.ChangeLocation(resolved.Value, request.BaseRevision);
    }

    private async Task<Result> ApplyProviderAsync(
        Organization organization,
        UpdateOrganizationRequest request,
        UserId caller,
        CancellationToken cancellationToken)
    {
        if (request.ProviderId is not { } providerId)
        {
            return Result.Success();
        }

        // An empty identifier is how a body unlinks itself, and needs no permission on a provider
        // it is walking away from.
        if (providerId == Guid.Empty)
        {
            return organization.SetProvider(null, request.BaseRevision);
        }

        return await _providers.CanActOnBehalfAsync(providerId, caller, cancellationToken)
            ? organization.SetProvider(providerId, request.BaseRevision)
            : Result.Failure(CommunityLifeErrors.NotProviderAgent);
    }

    private static Result ApplyCollections(
        Organization organization, UpdateOrganizationRequest request)
    {
        if (request.Languages is not null)
        {
            var languages = request.Languages
                .Select((language, index) => OrganizationLanguage.Create(language, index))
                .ToList();

            var set = organization.SetLanguages(languages, request.BaseRevision);

            if (set.IsFailure)
            {
                return set;
            }
        }

        if (request.Services is null)
        {
            return Result.Success();
        }

        var services = request.Services
            .Select((service, index) => OrganizationService.Create(service, index))
            .ToList();

        return organization.SetServices(services, request.BaseRevision);
    }
}
