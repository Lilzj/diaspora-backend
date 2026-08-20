using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Analytics;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Memberships;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;
using Diaspora.Modules.CommunityLife.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>
/// Drafts an organization.
/// <para>
/// <b>The creator becomes its owner in the same transaction.</b> An organization exists to be run by
/// people, and a body whose owner membership was written afterwards would spend the gap
/// administrable by nobody — which is also why the authorization service accepts the creator without
/// a membership row rather than this handler writing the row first and hoping.
/// </para>
/// <para>
/// <b>Always lands as a private draft.</b> Publishing is a separate act with its own permission and
/// its own readiness checks — an organization page that went live the moment somebody typed a name
/// would put half-finished contact details in front of members looking for help.
/// </para>
/// <para>
/// <b>The analytics record is opened here rather than at publication.</b> Views on a draft are still
/// views — an organizer sharing an unlisted link to get an opinion generates them — and a record
/// created later would silently start counting from the wrong moment.
/// </para>
/// </summary>
public sealed class CreateOrganizationCommandHandler
    : ICommandHandler<CreateOrganizationCommand, OrganizationResponse>
{
    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationMembershipRepository _memberships;
    private readonly IOrganizationAnalyticsRepository _analytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityLifeCatalogResolver _catalog;
    private readonly CommunityLifeNeighborhoodResolver _neighborhoods;
    private readonly CommunityLifeProviderResolver _providers;
    private readonly CommunityLifeSlugAllocator _slugs;
    private readonly CommunityLifeMediaClaimer _media;
    private readonly OrganizationReadContext _read;
    private readonly IOptions<CommunityLifeOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreateOrganizationCommandHandler(
        IOrganizationRepository organizations,
        IOrganizationMembershipRepository memberships,
        IOrganizationAnalyticsRepository analytics,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        CommunityLifeCatalogResolver catalog,
        CommunityLifeNeighborhoodResolver neighborhoods,
        CommunityLifeProviderResolver providers,
        CommunityLifeSlugAllocator slugs,
        CommunityLifeMediaClaimer media,
        OrganizationReadContext read,
        IOptions<CommunityLifeOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _organizations = organizations;
        _memberships = memberships;
        _analytics = analytics;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _catalog = catalog;
        _neighborhoods = neighborhoods;
        _providers = providers;
        _slugs = slugs;
        _media = media;
        _read = read;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<OrganizationResponse>> Handle(
        CreateOrganizationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<OrganizationResponse>(caller.Error);
        }

        var request = command.Request;
        var now = _clock.UtcNow;

        var guard = await GuardAsync(caller.Value, request, cancellationToken);

        if (guard.IsFailure)
        {
            return Result.Failure<OrganizationResponse>(guard.Error);
        }

        var type = await _catalog.RequireTypeAsync(
            request.OrganizationTypeKey, cancellationToken: cancellationToken);

        if (type.IsFailure)
        {
            return Result.Failure<OrganizationResponse>(type.Error);
        }

        var content = BuildContent(request);

        if (content.IsFailure)
        {
            return Result.Failure<OrganizationResponse>(content.Error);
        }

        var location = await BuildLocationAsync(request, cancellationToken);

        if (location.IsFailure)
        {
            return Result.Failure<OrganizationResponse>(location.Error);
        }

        var organization = Organization.Draft(
            OrganizationId.From(_guids.NewSequentialGuid()),
            caller.Value,
            content.Value.Name,
            await _slugs.ForOrganizationAsync(request.Name, cancellationToken),
            content.Value.Description,
            type.Value.Key,
            location.Value,
            now);

        if (organization.IsFailure)
        {
            return Result.Failure<OrganizationResponse>(organization.Error);
        }

        var applied = await ApplyDetailAsync(
            organization.Value, request, content.Value, caller.Value, cancellationToken);

        if (applied.IsFailure)
        {
            return Result.Failure<OrganizationResponse>(applied.Error);
        }

        await _organizations.AddAsync(organization.Value, cancellationToken);

        var owner = OpenOwnerMembership(organization.Value, caller.Value, now);

        if (owner.IsFailure)
        {
            return Result.Failure<OrganizationResponse>(owner.Error);
        }

        await _memberships.AddAsync(owner.Value, cancellationToken);

        // Opened now rather than at publication. See the class summary.
        await _analytics.AddAsync(
            OrganizationAnalytics.Open(
                OrganizationAnalyticsId.From(_guids.NewSequentialGuid()),
                organization.Value.Id,
                location.Value.CountryCode,
                type.Value.Key,
                location.Value.NeighborhoodAreaId,
                now),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // After the save, so an unclaimed asset is the worst outcome of a Media outage rather than
        // a refused organization.
        await ClaimMediaAsync(organization.Value, request, cancellationToken);

        return Result.Success(
            await _read.ToResponseAsync(organization.Value, cancellationToken));
    }

    /// <summary>
    /// The two refusals that are about the creator rather than the content.
    /// <para>
    /// The duplicate check is exact-name within one creator. Fuzzy matching belongs to the spam
    /// seam, where a false positive is a moderator's note rather than a refusal — two genuinely
    /// different associations in one diaspora often share most of a name.
    /// </para>
    /// </summary>
    private async Task<Result> GuardAsync(
        UserId caller,
        CreateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        var held = await _organizations.CountActiveByCreatorAsync(caller, cancellationToken);

        if (held >= _options.Value.MaxOrganizationsPerCreator)
        {
            return Result.Failure(CommunityLifeErrors.TooManyOrganizations);
        }

        return await _organizations.HasDuplicateAsync(
            caller, request.Name, excluding: null, cancellationToken)
            ? Result.Failure(CommunityLifeErrors.DuplicateOrganization)
            : Result.Success();
    }

    /// <summary>
    /// Builds the value objects the aggregate is constructed from.
    /// <para>
    /// Separated because they fail independently, and a caller who sent a malformed email should be
    /// told about the email rather than about whichever check happened to run first.
    /// </para>
    /// </summary>
    private static Result<OrganizationContent> BuildContent(CreateOrganizationRequest request)
    {
        var name = OrganizationName.Create(request.Name, request.ShortName);

        if (name.IsFailure)
        {
            return Result.Failure<OrganizationContent>(name.Error);
        }

        var description = CommunityLifeDescription.Create(request.Summary, request.Description);

        if (description.IsFailure)
        {
            return Result.Failure<OrganizationContent>(description.Error);
        }

        var contact = ContactDetails.Create(
            request.Contact?.Phone,
            request.Contact?.WhatsApp,
            request.Contact?.Email,
            request.Contact?.Website,
            request.Contact?.ContactName);

        if (contact.IsFailure)
        {
            return Result.Failure<OrganizationContent>(contact.Error);
        }

        var social = SocialLinks.Create(
            request.SocialLinks?.Facebook,
            request.SocialLinks?.Instagram,
            request.SocialLinks?.Twitter,
            request.SocialLinks?.YouTube,
            request.SocialLinks?.LinkedIn,
            request.SocialLinks?.TikTok);

        if (social.IsFailure)
        {
            return Result.Failure<OrganizationContent>(social.Error);
        }

        var policy = MembershipPolicy.Create(
            CommunityLifeSharedMapper.ParseEnum<MembershipPolicyKind>(request.MembershipPolicy)
                ?? MembershipPolicyKind.Open,
            request.MembershipRequirements,
            renewalMonths: null,
            request.IsMembershipPublic);

        return policy.IsFailure
            ? Result.Failure<OrganizationContent>(policy.Error)
            : Result.Success(new OrganizationContent(
                name.Value, description.Value, contact.Value, social.Value, policy.Value));
    }

    /// <summary>
    /// Resolves the geography through B4.
    /// <para>
    /// The neighbourhood, when one is named, is resolved against Location's catalogue rather than
    /// taken from the request — so a body is placed in a real neighbourhood and inherits B4's name,
    /// path and country rather than whatever the client typed.
    /// </para>
    /// </summary>
    private async Task<Result<CommunityLifeLocationContext>> BuildLocationAsync(
        CreateOrganizationRequest request, CancellationToken cancellationToken)
    {
        var context = CommunityLifeLocationContext.Create(
            request.CountryCode, request.RegionName, request.CityName);

        if (context.IsFailure)
        {
            return context;
        }

        var resolved = await _neighborhoods.ResolveAsync(
            context.Value, request.NeighborhoodAreaId, cancellationToken);

        if (resolved.IsFailure)
        {
            return resolved;
        }

        // Where no neighbourhood was named, the country still has to be one B4 knows about.
        return request.NeighborhoodAreaId is null
            && !await _neighborhoods.CountryExistsAsync(request.CountryCode, cancellationToken)
            ? Result.Failure<CommunityLifeLocationContext>(CommunityLifeErrors.CountryInvalid)
            : resolved;
    }

    /// <summary>Applies the collections and optional detail the aggregate takes separately.</summary>
    private async Task<Result> ApplyDetailAsync(
        Organization organization,
        CreateOrganizationRequest request,
        OrganizationContent content,
        UserId caller,
        CancellationToken cancellationToken)
    {
        var updated = organization.UpdateContent(
            baseRevision: null,
            caller,
            mission: request.Mission,
            vision: request.Vision,
            contact: content.Contact,
            socialLinks: content.SocialLinks,
            membershipPolicy: content.MembershipPolicy);

        if (updated.IsFailure)
        {
            return Result.Failure(updated.Error);
        }

        if (request.Languages is { Count: > 0 })
        {
            var languages = request.Languages
                .Select((language, index) => OrganizationLanguage.Create(language, index))
                .ToList();

            var set = organization.SetLanguages(languages, baseRevision: null);

            if (set.IsFailure)
            {
                return set;
            }
        }

        if (request.Services is { Count: > 0 })
        {
            var services = request.Services
                .Select((service, index) => OrganizationService.Create(service, index))
                .ToList();

            var set = organization.SetServices(services, baseRevision: null);

            if (set.IsFailure)
            {
                return set;
            }
        }

        var provider = await ApplyProviderAsync(organization, request, caller, cancellationToken);

        if (provider.IsFailure)
        {
            return provider;
        }

        return AttachMedia(organization, request);
    }

    /// <summary>
    /// Links the body to a B7 provider listing, if the caller may act for it.
    /// <para>
    /// Checked rather than trusted: a provider identifier the caller does not represent would let
    /// anybody hang their organization off somebody else's verified business.
    /// </para>
    /// </summary>
    private async Task<Result> ApplyProviderAsync(
        Organization organization,
        CreateOrganizationRequest request,
        UserId caller,
        CancellationToken cancellationToken)
    {
        if (request.ProviderId is not { } providerId || providerId == Guid.Empty)
        {
            return Result.Success();
        }

        return await _providers.CanActOnBehalfAsync(providerId, caller, cancellationToken)
            ? organization.SetProvider(providerId, baseRevision: null)
            : Result.Failure(CommunityLifeErrors.NotProviderAgent);
    }

    private Result AttachMedia(Organization organization, CreateOrganizationRequest request)
    {
        if (request.Media is not { Count: > 0 })
        {
            return Result.Success();
        }

        var now = _clock.UtcNow;

        foreach (var media in request.Media)
        {
            var role = CommunityLifeSharedMapper.ParseEnum<CommunityLifeMediaRole>(media.Role)
                ?? CommunityLifeMediaRole.Gallery;
            var attached = organization.AttachMedia(media.MediaId, role, media.Caption, now);

            if (attached.IsFailure)
            {
                return attached;
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// The creator's owner membership, opened active and with no renewal.
    /// <para>
    /// An owner whose membership could lapse is an organization that becomes unadministrable while
    /// nobody is looking, which is why the renewal months the body may set for everybody else are
    /// deliberately not applied here.
    /// </para>
    /// </summary>
    private Result<OrganizationMembership> OpenOwnerMembership(
        Organization organization, UserId caller, DateTimeOffset now) =>
        OrganizationMembership.Open(
            OrganizationMembershipId.From(_guids.NewSequentialGuid()),
            organization.Id,
            caller,
            MembershipStatus.Active,
            MembershipRole.Owner,
            _guids.NewSequentialGuid(),
            now);

    private async Task ClaimMediaAsync(
        Organization organization,
        CreateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Media is not { Count: > 0 })
        {
            return;
        }

        await _media.ClaimManyForOrganizationAsync(
            organization.Id.Value,
            [.. request.Media.Select(media => (media.MediaId, media.Role))],
            cancellationToken);
    }

    private sealed record OrganizationContent(
        OrganizationName Name,
        CommunityLifeDescription Description,
        ContactDetails Contact,
        SocialLinks SocialLinks,
        MembershipPolicy MembershipPolicy);
}
