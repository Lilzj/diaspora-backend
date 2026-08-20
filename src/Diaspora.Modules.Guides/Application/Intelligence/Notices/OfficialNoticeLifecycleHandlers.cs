using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Intelligence.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Intelligence.Notices;

/// <summary>Sends a completed draft for verification.</summary>
public sealed class SubmitOfficialNoticeCommandHandler
    : ICommandHandler<SubmitOfficialNoticeCommand, OfficialNoticeResponse>
{
    private readonly IOfficialNoticeRepository _notices;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IntelligenceAuthorizationService _authorization;
    private readonly OfficialNoticePresenter _presenter;

    public SubmitOfficialNoticeCommandHandler(
        IOfficialNoticeRepository notices,
        IUnitOfWork unitOfWork,
        IntelligenceAuthorizationService authorization,
        OfficialNoticePresenter presenter)
    {
        _notices = notices;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _presenter = presenter;
    }

    public async Task<Result<OfficialNoticeResponse>> Handle(
        SubmitOfficialNoticeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var notice = await _notices.GetAsync(
            OfficialNoticeId.From(command.OfficialNoticeId), cancellationToken);

        if (notice is null)
        {
            return Result.Failure<OfficialNoticeResponse>(IntelligenceErrors.NoticeNotFound);
        }

        var revision = notice.EnsureRevision(command.ExpectedRevision);

        if (revision.IsFailure)
        {
            return Result.Failure<OfficialNoticeResponse>(revision.Error);
        }

        var submitted = notice.Submit(_authorization.CallerId);

        if (submitted.IsFailure)
        {
            return Result.Failure<OfficialNoticeResponse>(submitted.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            await _presenter.PresentAsync(
                notice, forEditor: true, cancellationToken: cancellationToken));
    }
}

/// <summary>
/// A verifier's decision.
/// <para>
/// A refusal returns the notice to draft rather than to a terminal rejected state, because the
/// ordinary cause is a missing reference number rather than a fabrication — and a workflow that made
/// the editor start again would lose the body they had already transcribed.
/// </para>
/// </summary>
public sealed class VerifyOfficialNoticeCommandHandler
    : ICommandHandler<VerifyOfficialNoticeCommand, OfficialNoticeResponse>
{
    private readonly IOfficialNoticeRepository _notices;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IntelligenceAuthorizationService _authorization;
    private readonly OfficialNoticePresenter _presenter;
    private readonly IClock _clock;

    public VerifyOfficialNoticeCommandHandler(
        IOfficialNoticeRepository notices,
        IUnitOfWork unitOfWork,
        IntelligenceAuthorizationService authorization,
        OfficialNoticePresenter presenter,
        IClock clock)
    {
        _notices = notices;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _presenter = presenter;
        _clock = clock;
    }

    public async Task<Result<OfficialNoticeResponse>> Handle(
        VerifyOfficialNoticeCommand command, CancellationToken cancellationToken)
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

        var now = _clock.UtcNow;
        var verifier = _authorization.CallerId;

        var decided = command.Request.Approved
            ? notice.MarkVerified(now, verifier, command.Request.Note)
            : notice.MarkRejected(now, verifier, command.Request.Note);

        if (decided.IsFailure)
        {
            return Result.Failure<OfficialNoticeResponse>(decided.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            await _presenter.PresentAsync(
                notice, forEditor: true, cancellationToken: cancellationToken));
    }
}

/// <summary>
/// Makes a verified notice visible.
/// <para>
/// <b>Two gates run here and nowhere else, and both are checked at publication rather than at
/// drafting because both can change in between.</b> The authority must be verified — that single
/// rule is what the word "official" on the surface is worth — and every registered source validator
/// must accept the sourcing. A deployment that tightened its source policy this morning applies it to
/// a notice drafted last week, which is the correct behaviour and the reason the check is late.
/// </para>
/// </summary>
public sealed class PublishOfficialNoticeCommandHandler
    : ICommandHandler<PublishOfficialNoticeCommand, OfficialNoticeResponse>
{
    private readonly IOfficialNoticeRepository _notices;
    private readonly IIssuingAuthorityRepository _authorities;
    private readonly IntelligenceSourceValidator _sources;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IntelligenceAuthorizationService _authorization;
    private readonly OfficialNoticePresenter _presenter;
    private readonly IClock _clock;

    public PublishOfficialNoticeCommandHandler(
        IOfficialNoticeRepository notices,
        IIssuingAuthorityRepository authorities,
        IntelligenceSourceValidator sources,
        IUnitOfWork unitOfWork,
        IntelligenceAuthorizationService authorization,
        OfficialNoticePresenter presenter,
        IClock clock)
    {
        _notices = notices;
        _authorities = authorities;
        _sources = sources;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _presenter = presenter;
        _clock = clock;
    }

    public async Task<Result<OfficialNoticeResponse>> Handle(
        PublishOfficialNoticeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var notice = await _notices.GetAsync(
            OfficialNoticeId.From(command.OfficialNoticeId), cancellationToken);

        if (notice is null)
        {
            return Result.Failure<OfficialNoticeResponse>(IntelligenceErrors.NoticeNotFound);
        }

        var revision = notice.EnsureRevision(command.ExpectedRevision);

        if (revision.IsFailure)
        {
            return Result.Failure<OfficialNoticeResponse>(revision.Error);
        }

        var authority = await _authorities.GetAsync(notice.AuthorityId, cancellationToken);

        if (authority is null)
        {
            return Result.Failure<OfficialNoticeResponse>(IntelligenceErrors.AuthorityNotFound);
        }

        // The gate the whole phase rests on. No option turns it off.
        if (!authority.IsVerified)
        {
            return Result.Failure<OfficialNoticeResponse>(
                IntelligenceErrors.AuthorityUnverified);
        }

        var sourced = await _sources.ValidateAsync(notice, authority, cancellationToken);

        if (sourced.IsFailure)
        {
            return Result.Failure<OfficialNoticeResponse>(sourced.Error);
        }

        var published = notice.Publish(_clock.UtcNow, _authorization.CallerId);

        if (published.IsFailure)
        {
            return Result.Failure<OfficialNoticeResponse>(published.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            await _presenter.PresentAsync(
                notice, forEditor: true, cancellationToken: cancellationToken));
    }
}

/// <summary>
/// Replaces a live notice with a later one.
/// <para>
/// <b>The replacement must itself be published, and must be about the same country.</b> A supersede
/// pointing at a draft would send everybody who followed the original to a page they cannot see; one
/// pointing at another country's notice is a transcription error that reads perfectly well until
/// somebody follows it.
/// </para>
/// </summary>
public sealed class SupersedeOfficialNoticeCommandHandler
    : ICommandHandler<SupersedeOfficialNoticeCommand, OfficialNoticeResponse>
{
    private readonly IOfficialNoticeRepository _notices;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IntelligenceAuthorizationService _authorization;
    private readonly OfficialNoticePresenter _presenter;
    private readonly IClock _clock;

    public SupersedeOfficialNoticeCommandHandler(
        IOfficialNoticeRepository notices,
        IUnitOfWork unitOfWork,
        IntelligenceAuthorizationService authorization,
        OfficialNoticePresenter presenter,
        IClock clock)
    {
        _notices = notices;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _presenter = presenter;
        _clock = clock;
    }

    public async Task<Result<OfficialNoticeResponse>> Handle(
        SupersedeOfficialNoticeCommand command, CancellationToken cancellationToken)
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

        var replacementId = OfficialNoticeId.From(command.Request.ReplacementNoticeId);
        var replacement = await _notices.GetAsync(replacementId, cancellationToken);

        if (replacement is null)
        {
            return Result.Failure<OfficialNoticeResponse>(IntelligenceErrors.NoticeNotFound);
        }

        if (replacement.Status != Domain.Enums.NoticeStatus.Published)
        {
            return Result.Failure<OfficialNoticeResponse>(
                IntelligenceErrors.SupersedeNotPublished);
        }

        if (!string.Equals(
            replacement.CountryCode, notice.CountryCode, StringComparison.Ordinal))
        {
            return Result.Failure<OfficialNoticeResponse>(
                IntelligenceErrors.AuthorityCountryMismatch);
        }

        var superseded = notice.Supersede(
            replacementId, _clock.UtcNow, _authorization.CallerId);

        if (superseded.IsFailure)
        {
            return Result.Failure<OfficialNoticeResponse>(superseded.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            await _presenter.PresentAsync(
                notice, forEditor: true, cancellationToken: cancellationToken));
    }
}

/// <summary>
/// Withdraws a notice.
/// <para>
/// The handler marks the aggregate for removal and saves; the shared context converts that into a
/// soft delete, stamps the timestamp and the actor, and leaves the row where every audit and every
/// restore can find it. Stamping it by hand is the defect B1.0.1 records, and it is silent — a
/// hand-stamped delete looks correct in the response either way.
/// </para>
/// </summary>
public sealed class ArchiveOfficialNoticeCommandHandler
    : ICommandHandler<ArchiveOfficialNoticeCommand>
{
    private readonly IOfficialNoticeRepository _notices;
    private readonly IUnitOfWork _unitOfWork;

    public ArchiveOfficialNoticeCommandHandler(
        IOfficialNoticeRepository notices, IUnitOfWork unitOfWork)
    {
        _notices = notices;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        ArchiveOfficialNoticeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var notice = await _notices.GetAsync(
            OfficialNoticeId.From(command.OfficialNoticeId), cancellationToken);

        if (notice is null)
        {
            return Result.Failure(IntelligenceErrors.NoticeNotFound);
        }

        _notices.Remove(notice);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

/// <summary>
/// Returns an archived notice to the desk.
/// <para>
/// <b>It reads through <c>GetIncludingArchivedAsync</c>, and that is the whole point of the
/// member.</b> Archiving is a soft delete, so the global query filter hides exactly the row a restore
/// needs — the blind spot the platform has recorded once and paid for once. An ordinary read here
/// could only ever answer "not found", which is the one answer certainly wrong for a notice the
/// editor can see in their own archive list.
/// </para>
/// </summary>
public sealed class RestoreOfficialNoticeCommandHandler
    : ICommandHandler<RestoreOfficialNoticeCommand, OfficialNoticeResponse>
{
    private readonly IOfficialNoticeRepository _notices;
    private readonly IUnitOfWork _unitOfWork;
    private readonly OfficialNoticePresenter _presenter;

    public RestoreOfficialNoticeCommandHandler(
        IOfficialNoticeRepository notices,
        IUnitOfWork unitOfWork,
        OfficialNoticePresenter presenter)
    {
        _notices = notices;
        _unitOfWork = unitOfWork;
        _presenter = presenter;
    }

    public async Task<Result<OfficialNoticeResponse>> Handle(
        RestoreOfficialNoticeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var notice = await _notices.GetIncludingArchivedAsync(
            OfficialNoticeId.From(command.OfficialNoticeId), cancellationToken);

        if (notice is null)
        {
            return Result.Failure<OfficialNoticeResponse>(IntelligenceErrors.NoticeNotFound);
        }

        if (!notice.IsDeleted)
        {
            return Result.Failure<OfficialNoticeResponse>(IntelligenceErrors.NoticeNotArchived);
        }

        notice.Restore();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            await _presenter.PresentAsync(
                notice, forEditor: true, cancellationToken: cancellationToken));
    }
}

/// <summary>Records that somebody checked a notice is still true.</summary>
public sealed class ReviewOfficialNoticeCommandHandler
    : ICommandHandler<ReviewOfficialNoticeCommand, OfficialNoticeResponse>
{
    private readonly IOfficialNoticeRepository _notices;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IntelligenceAuthorizationService _authorization;
    private readonly OfficialNoticePresenter _presenter;
    private readonly IClock _clock;

    public ReviewOfficialNoticeCommandHandler(
        IOfficialNoticeRepository notices,
        IUnitOfWork unitOfWork,
        IntelligenceAuthorizationService authorization,
        OfficialNoticePresenter presenter,
        IClock clock)
    {
        _notices = notices;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _presenter = presenter;
        _clock = clock;
    }

    public async Task<Result<OfficialNoticeResponse>> Handle(
        ReviewOfficialNoticeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var notice = await _notices.GetAsync(
            OfficialNoticeId.From(command.OfficialNoticeId), cancellationToken);

        if (notice is null)
        {
            return Result.Failure<OfficialNoticeResponse>(IntelligenceErrors.NoticeNotFound);
        }

        var reviewed = notice.MarkReviewed(_clock.UtcNow, _authorization.CallerId);

        if (reviewed.IsFailure)
        {
            return Result.Failure<OfficialNoticeResponse>(reviewed.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            await _presenter.PresentAsync(
                notice, forEditor: true, cancellationToken: cancellationToken));
    }
}
