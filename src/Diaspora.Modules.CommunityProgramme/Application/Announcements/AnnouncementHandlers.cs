using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Application.Mapping;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Requests;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Announcements;
using Diaspora.Modules.CommunityProgramme.Domain.Capabilities;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.CommunityProgramme.Application.Announcements;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// COMMUNITY-LIFE-GAP-016 — community announcements.
//
// The register's framing: "B9 posts are authored by a member to a country audience. There is no scope
// on them, so an organisation cannot address its members, and there is no audience-resolution rule to
// reuse." This gap is that scope and that rule, and nothing else — delivery stays B13's, and this
// module writes none of it.
//
// The audience rule is applied in the query rather than after it. An organization announcement
// reaches its members and nobody else, and a caller who is not one never receives the row.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Publishes a scoped announcement.</summary>
[Authorize(Permission = CommunityProgrammePermissions.PublishAnnouncements)]
[Transactional(Reason =
    "Writes the announcement and its media references as one unit, and raises the publication event "
    + "inside the same transaction so B13 cannot fan out a post that did not commit.")]
public sealed record PublishAnnouncementCommand : ICommand<CommunityAnnouncementResponse>
{
    public required PublishAnnouncementRequest Request { get; init; }

    public string? IdempotencyKeyHeader { get; init; }
}

public sealed class PublishAnnouncementCommandValidator
    : AbstractValidator<PublishAnnouncementCommand>
{
    public PublishAnnouncementCommandValidator()
    {
        RuleFor(command => command.Request).NotNull();
        RuleFor(command => command.Request.Scope).NotEmpty();

        RuleFor(command => command.Request.Title)
            .NotEmpty()
            .MaximumLength(CommunityProgrammeLimits.MaxNameLength);

        RuleFor(command => command.Request.Body)
            .NotEmpty()
            .MaximumLength(CommunityProgrammeLimits.MaxDescriptionLength);

        RuleFor(command => command.Request.MediaIds!.Count)
            .LessThanOrEqualTo(CommunityProgrammeLimits.MaxMediaReferences)
            .When(command => command.Request.MediaIds is not null);
    }
}

public sealed class PublishAnnouncementCommandHandler
    : ICommandHandler<PublishAnnouncementCommand, CommunityAnnouncementResponse>
{
    private const string Operation = "announcement";

    private readonly ICommunityAnnouncementRepository _announcements;
    private readonly CommunityProgrammeAccessService _access;
    private readonly ProgrammeIdempotency _commit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public PublishAnnouncementCommandHandler(
        ICommunityAnnouncementRepository announcements,
        CommunityProgrammeAccessService access,
        ProgrammeIdempotency commit,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock)
    {
        _announcements = announcements;
        _access = access;
        _commit = commit;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<CommunityAnnouncementResponse>> Handle(
        PublishAnnouncementCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CommunityAnnouncementResponse>(caller.Error);
        }

        if (!Enum.TryParse<AnnouncementScope>(
            command.Request.Scope, ignoreCase: true, out var scope))
        {
            return Result.Failure<CommunityAnnouncementResponse>(
                CommunityProgrammeErrors.Validation(
                    $"'{command.Request.Scope}' is not a scope this platform recognises."));
        }

        var key = ProgrammeIdempotency.Normalise(
            Operation, caller.Value.Value, command.IdempotencyKeyHeader, command.Request.IdempotencyKey);

        if (key is not null)
        {
            var replayed = await _announcements.FindByIdempotencyKeyAsync(
                caller.Value, key, cancellationToken);

            if (replayed is not null)
            {
                // The caller's own key, spent on an announcement somewhere else. Replaying it would
                // answer for the wrong place; publishing would break the key's promise.
                if (replayed.Scope != scope || replayed.ScopeId != command.Request.ScopeId)
                {
                    return Result.Failure<CommunityAnnouncementResponse>(
                        CommunityProgrammeErrors.IdempotencyKeyReused);
                }

                return await RenderAsync(replayed, cancellationToken);
            }
        }

        var organizationId = await ResolveOrganizationAsync(scope, command, cancellationToken);

        if (organizationId.IsFailure)
        {
            return Result.Failure<CommunityAnnouncementResponse>(organizationId.Error);
        }

        var published = CommunityAnnouncement.Publish(
            CommunityAnnouncementId.From(_guids.NewSequentialGuid()),
            scope,
            command.Request.ScopeId,
            organizationId.Value,
            caller.Value,
            command.Request.Title,
            command.Request.Body,
            command.Request.CountryCode,
            command.Request.IsPinned,
            _clock.UtcNow,
            command.Request.MediaIds,
            key);

        if (published.IsFailure)
        {
            return Result.Failure<CommunityAnnouncementResponse>(published.Error);
        }

        _announcements.Add(published.Value);

        var committed = await ProgrammeIdempotency.GuardAsync(
            key,
            token => _unitOfWork.SaveChangesAsync(token),
            token => key is null
                ? Task.FromResult<CommunityAnnouncement?>(null)
                : _announcements.FindByIdempotencyKeyAsync(caller.Value, key, token),
            cancellationToken,
            inTransaction: true);

        if (committed.IsFailure)
        {
            return Result.Failure<CommunityAnnouncementResponse>(committed.Error);
        }

        return await RenderAsync(committed.Value.Replayed ?? published.Value, cancellationToken);
    }

    /// <summary>
    /// Which organization is answerable for the words, and whether the caller may speak for it.
    /// <para>
    /// <b>An organization announcement is the only scope this module can authorize today, and the
    /// others are refused rather than allowed on trust.</b> A neighbourhood announcement would need
    /// B4 to answer "may this person address this area", an event announcement would need B12's
    /// organizer check, and a campaign announcement would need B15.1's campaign to exist. None of the
    /// three has an owner yet, and publishing to a scope nobody can gate would be a platform-wide
    /// megaphone. The scopes stay in the contract so the day each acquires an owner is a handler
    /// change rather than a schema change.
    /// </para>
    /// </summary>
    private async Task<Result<Guid?>> ResolveOrganizationAsync(
        AnnouncementScope scope,
        PublishAnnouncementCommand command,
        CancellationToken cancellationToken)
    {
        if (scope != AnnouncementScope.Organization)
        {
            return Result.Failure<Guid?>(CommunityProgrammeErrors.Validation(
                "Only an organization can publish an announcement today. Neighbourhood, event and "
                + "campaign announcements need an owner that can say who may address them."));
        }

        if (command.Request.ScopeId is not { } organizationId || organizationId == Guid.Empty)
        {
            return Result.Failure<Guid?>(CommunityProgrammeErrors.AnnouncementScopeInvalid);
        }

        var permitted = await _access.RequireCapabilityAsync(
            organizationId, OrganizationCapability.PublishAnnouncements, cancellationToken);

        return permitted.IsFailure
            ? Result.Failure<Guid?>(permitted.Error)
            : Result.Success<Guid?>(organizationId);
    }

    private async Task<Result<CommunityAnnouncementResponse>> RenderAsync(
        CommunityAnnouncement announcement, CancellationToken cancellationToken)
    {
        var organization = announcement.OrganizationId is { } id
            ? await _access.FindOrganizationAsync(id, cancellationToken)
            : null;

        return Result.Success(CommunityProgrammeMapper.ToResponse(
            announcement, organization?.Name ?? "Diaspora"));
    }
}

/// <summary>
/// The announcement feed.
/// <para>
/// The audience rule is in the query: an organization's announcement is selected only for a member of
/// that organization. A caller who is not one does not receive the row and does not learn it exists.
/// </para>
/// </summary>
[Authorize(Permission = CommunityProgrammePermissions.Read)]
public sealed record ListAnnouncementsQuery
    : IQuery<CommunityPageResponse<CommunityAnnouncementResponse>>
{
    public string? Scope { get; init; }

    public Guid? ScopeId { get; init; }

    public string? CountryCode { get; init; }

    public string? Query { get; init; }

    public string? Cursor { get; init; }

    public int? Take { get; init; }
}

public sealed class ListAnnouncementsQueryHandler
    : IQueryHandler<ListAnnouncementsQuery, CommunityPageResponse<CommunityAnnouncementResponse>>
{
    private readonly ICommunityAnnouncementRepository _announcements;
    private readonly CommunityProgrammeAccessService _access;

    public ListAnnouncementsQueryHandler(
        ICommunityAnnouncementRepository announcements, CommunityProgrammeAccessService access)
    {
        _announcements = announcements;
        _access = access;
    }

    public async Task<Result<CommunityPageResponse<CommunityAnnouncementResponse>>> Handle(
        ListAnnouncementsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var memberOf = await _access.MemberOfAsync(cancellationToken);

        var page = await _announcements.SearchAsync(
            new AnnouncementQueryFilter
            {
                Scope = Enum.TryParse<AnnouncementScope>(
                    query.Scope, ignoreCase: true, out var scope)
                        ? scope
                        : null,
                ScopeId = query.ScopeId,
                CountryCode = query.CountryCode,
                MemberOfOrganizationIds = memberOf,
                SearchTerm = query.Query,
                Cursor = query.Cursor,
                Limit = CommunityProgrammeLimits.PageSize(query.Take)
            },
            cancellationToken);

        if (page.Items.Count == 0)
        {
            return Result.Success(new CommunityPageResponse<CommunityAnnouncementResponse>
            {
                Items = [],
                HasMore = false
            });
        }

        var organizations = await _access.FindOrganizationsAsync(
            [.. page.Items.Where(announcement => announcement.OrganizationId is not null)
                .Select(announcement => announcement.OrganizationId!.Value)],
            cancellationToken);

        return Result.Success(new CommunityPageResponse<CommunityAnnouncementResponse>
        {
            Items =
            [
                .. page.Items.Select(announcement => CommunityProgrammeMapper.ToResponse(
                    announcement,
                    announcement.OrganizationId is { } id
                        ? organizations.GetValueOrDefault(id)?.Name ?? "Diaspora"
                        : "Diaspora"))
            ],
            NextCursor = page.NextCursor,
            HasMore = page.HasMore
        });
    }
}
