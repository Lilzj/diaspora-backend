using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Intelligence.Services;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Intelligence.Notices;

/// <summary>
/// Opens a draft.
/// <para>
/// <b>Three checks before the aggregate sees anything: the category exists and is active, the
/// authority exists, and it issues for this country.</b> The last is the one worth having: an
/// immigration department in Mauritius attached to a notice about Nigeria is a mistake that reads
/// perfectly well on an editing screen and is obvious nowhere until somebody in Lagos is told their
/// visa rules changed by a ministry that does not govern them.
/// </para>
/// <para>
/// The severity defaults to the category's, which is right nearly always: an editor filing under
/// "Emergency Alerts" means critical, and making them say so twice is a step they will eventually
/// forget on the day it matters.
/// </para>
/// </summary>
public sealed class CreateOfficialNoticeCommandHandler
    : ICommandHandler<CreateOfficialNoticeCommand, OfficialNoticeResponse>
{
    private readonly IOfficialNoticeRepository _notices;
    private readonly INoticeCategoryRepository _categories;
    private readonly IIssuingAuthorityRepository _authorities;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IntelligenceAuthorizationService _authorization;
    private readonly OfficialNoticePresenter _presenter;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreateOfficialNoticeCommandHandler(
        IOfficialNoticeRepository notices,
        INoticeCategoryRepository categories,
        IIssuingAuthorityRepository authorities,
        IUnitOfWork unitOfWork,
        IntelligenceAuthorizationService authorization,
        OfficialNoticePresenter presenter,
        IGuidGenerator guids,
        IClock clock)
    {
        _notices = notices;
        _categories = categories;
        _authorities = authorities;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _presenter = presenter;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<OfficialNoticeResponse>> Handle(
        CreateOfficialNoticeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;

        var category = await _categories.GetByKeyAsync(request.CategoryKey, cancellationToken);

        if (category is null)
        {
            return Result.Failure<OfficialNoticeResponse>(IntelligenceErrors.CategoryNotFound);
        }

        var authorityId = IssuingAuthorityId.From(request.AuthorityId);
        var authority = await _authorities.GetAsync(authorityId, cancellationToken);

        if (authority is null)
        {
            return Result.Failure<OfficialNoticeResponse>(IntelligenceErrors.AuthorityNotFound);
        }

        if (!authority.IssuesFor(request.CountryCode))
        {
            return Result.Failure<OfficialNoticeResponse>(
                IntelligenceErrors.AuthorityCountryMismatch);
        }

        var severity = IntelligenceWireNames.ParseSeverity(request.Severity)
            ?? category.DefaultSeverity;

        var caller = _authorization.CallerId;

        var created = OfficialNotice.Create(
            OfficialNoticeId.From(_guids.NewSequentialGuid()),
            request.CountryCode,
            category.Key,
            authorityId,
            request.Title,
            request.Summary,
            request.Content,
            severity,
            _clock.UtcNow,
            caller);

        if (created.IsFailure)
        {
            return Result.Failure<OfficialNoticeResponse>(created.Error);
        }

        await _notices.AddAsync(created.Value, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            await _presenter.PresentAsync(
                created.Value, forEditor: true, cancellationToken: cancellationToken));
    }
}

/// <summary>
/// Rewrites a notice.
/// <para>
/// <b>Every field is optional and the handler applies only what was sent</b>, stopping at the first
/// refusal. Stopping rather than collecting means the editor gets the specific code — an unusable
/// locale and a locked notice are different mistakes with different fixes — and the
/// <c>[Transactional]</c> on the command is what makes the stop safe: nothing that was applied before
/// the refusal survives it.
/// </para>
/// <para>
/// <b>The revision is checked first, before anything is applied.</b> Checking it after would let a
/// stale editor's content land and then be rejected, which on a retrying client is how two editors
/// take turns overwriting each other while both see a conflict.
/// </para>
/// </summary>
public sealed class UpdateOfficialNoticeCommandHandler
    : ICommandHandler<UpdateOfficialNoticeCommand, OfficialNoticeResponse>
{
    private readonly IOfficialNoticeRepository _notices;
    private readonly INoticeCategoryRepository _categories;
    private readonly IIssuingAuthorityRepository _authorities;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IntelligenceAuthorizationService _authorization;
    private readonly OfficialNoticePresenter _presenter;
    private readonly IClock _clock;

    public UpdateOfficialNoticeCommandHandler(
        IOfficialNoticeRepository notices,
        INoticeCategoryRepository categories,
        IIssuingAuthorityRepository authorities,
        IUnitOfWork unitOfWork,
        IntelligenceAuthorizationService authorization,
        OfficialNoticePresenter presenter,
        IClock clock)
    {
        _notices = notices;
        _categories = categories;
        _authorities = authorities;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _presenter = presenter;
        _clock = clock;
    }

    public async Task<Result<OfficialNoticeResponse>> Handle(
        UpdateOfficialNoticeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var notice = await _notices.GetAsync(
            OfficialNoticeId.From(command.OfficialNoticeId), cancellationToken);

        if (notice is null)
        {
            return Result.Failure<OfficialNoticeResponse>(IntelligenceErrors.NoticeNotFound);
        }

        var revision = notice.EnsureRevision(command.Request.ExpectedRevision);

        if (revision.IsFailure)
        {
            return Result.Failure<OfficialNoticeResponse>(revision.Error);
        }

        var applied = await ApplyAsync(notice, command.Request, cancellationToken);

        if (applied.IsFailure)
        {
            return Result.Failure<OfficialNoticeResponse>(applied.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            await _presenter.PresentAsync(
                notice, forEditor: true, cancellationToken: cancellationToken));
    }

    private async Task<Result> ApplyAsync(
        OfficialNotice notice,
        UpdateOfficialNoticeRequest request,
        CancellationToken cancellationToken)
    {
        var caller = _authorization.CallerId;

        var content = ApplyContent(notice, request, caller);

        if (content.IsFailure)
        {
            return content;
        }

        var classification = await ApplyClassificationAsync(
            notice, request, caller, cancellationToken);

        if (classification.IsFailure)
        {
            return classification;
        }

        var authority = await ApplyAuthorityAsync(notice, request, caller, cancellationToken);

        if (authority.IsFailure)
        {
            return authority;
        }

        var dates = ApplyDates(notice, request, caller);

        if (dates.IsFailure)
        {
            return dates;
        }

        var source = ApplySource(notice, request, caller);

        return source.IsFailure ? source : ApplyCollections(notice, request, caller);
    }

    private static Result ApplyContent(
        OfficialNotice notice, UpdateOfficialNoticeRequest request, UserId? caller)
    {
        // All three or none. A request that rewrote the title and left the body would be an edit the
        // aggregate cannot express — its content operation takes the three together, because a
        // verified notice's verification covers all of it.
        if (request.Title is null && request.Summary is null && request.Content is null)
        {
            return Result.Success();
        }

        return notice.UpdateContent(
            request.Title ?? notice.Title,
            request.Summary ?? notice.Summary,
            request.Content ?? notice.Content,
            caller);
    }

    private async Task<Result> ApplyClassificationAsync(
        OfficialNotice notice,
        UpdateOfficialNoticeRequest request,
        UserId? caller,
        CancellationToken cancellationToken)
    {
        if (request.CategoryKey is null && request.Severity is null && request.Priority is null)
        {
            return Result.Success();
        }

        var categoryKey = request.CategoryKey ?? notice.CategoryKey;

        if (request.CategoryKey is not null)
        {
            var category = await _categories.GetByKeyAsync(categoryKey, cancellationToken);

            if (category is null)
            {
                return Result.Failure(IntelligenceErrors.CategoryNotFound);
            }
        }

        var severity = IntelligenceWireNames.ParseSeverity(request.Severity) ?? notice.Severity;

        return notice.Reclassify(
            categoryKey, severity, request.Priority ?? notice.Priority, caller);
    }

    private async Task<Result> ApplyAuthorityAsync(
        OfficialNotice notice,
        UpdateOfficialNoticeRequest request,
        UserId? caller,
        CancellationToken cancellationToken)
    {
        if (request.AuthorityId is not { } id)
        {
            return Result.Success();
        }

        var authorityId = IssuingAuthorityId.From(id);
        var authority = await _authorities.GetAsync(authorityId, cancellationToken);

        if (authority is null)
        {
            return Result.Failure(IntelligenceErrors.AuthorityNotFound);
        }

        return authority.IssuesFor(notice.CountryCode)
            ? notice.Reassign(authorityId, caller)
            : Result.Failure(IntelligenceErrors.AuthorityCountryMismatch);
    }

    private Result ApplyDates(
        OfficialNotice notice, UpdateOfficialNoticeRequest request, UserId? caller) =>
        request.SetDates
            ? notice.Schedule(
                request.PublicationDate,
                request.EffectiveFrom,
                request.ExpiresAt,
                _clock.UtcNow,
                caller)
            : Result.Success();

    private static Result ApplySource(
        OfficialNotice notice, UpdateOfficialNoticeRequest request, UserId? caller)
    {
        if (request.ReferenceNumber is null && request.SourceUrl is null && request.Region is null)
        {
            return Result.Success();
        }

        if (request.ReferenceNumber is not null || request.SourceUrl is not null)
        {
            var attributed = notice.AttributeSource(
                request.ReferenceNumber ?? notice.ReferenceNumber,
                request.SourceUrl ?? notice.SourceUrl,
                caller);

            if (attributed.IsFailure)
            {
                return attributed;
            }
        }

        return request.Region is null
            ? Result.Success()
            : notice.SetRegion(request.Region, caller);
    }

    private static Result ApplyCollections(
        OfficialNotice notice, UpdateOfficialNoticeRequest request, UserId? caller)
    {
        if (request.Languages is { } languages)
        {
            var applied = notice.SetLanguages(languages, caller);

            if (applied.IsFailure)
            {
                return applied;
            }
        }

        if (request.Localizations is { } localizations)
        {
            var applied = notice.SetLocalizations(
                [.. localizations.Select(localization =>
                    ((string?)localization.Locale,
                        (string?)localization.Title,
                        (string?)localization.Summary,
                        (string?)localization.Content))],
                caller);

            if (applied.IsFailure)
            {
                return applied;
            }
        }

        if (request.Attachments is { } attachments)
        {
            var applied = notice.SetAttachments(
                [.. attachments.Select(attachment =>
                    (attachment.MediaId, attachment.Caption))],
                caller);

            if (applied.IsFailure)
            {
                return applied;
            }
        }

        if (request.References is not { } references)
        {
            return Result.Success();
        }

        var resolved = new List<(NoticeReferenceKind, Guid, string?)>(references.Count);

        foreach (var reference in references)
        {
            var kind = IntelligenceWireNames.ParseReferenceKind(reference.Kind);

            if (kind is null)
            {
                return Result.Failure(IntelligenceErrors.ReferenceInvalid);
            }

            resolved.Add((kind.Value, reference.TargetId, reference.Label));
        }

        return notice.SetReferences(resolved, caller);
    }
}
