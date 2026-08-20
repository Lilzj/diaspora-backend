using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Application.Mapping;
using Diaspora.Modules.Creator.Application.Services;
using Diaspora.Modules.Creator.Contracts.V1.Requests;
using Diaspora.Modules.Creator.Contracts.V1.Responses;
using Diaspora.Modules.Creator.Domain.Errors;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Creator.Application.Creators;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// CREATOR-GAP-001 — the creator projection and its status.
//
// Three routes, and the register's whole point is the second word of the gap's title: a projection.
// "The platform knows a member has published guides and knows their reputation level, but nothing
// says 'this person is an Established Creator'." These handlers are what says it.
//
// The status is never computed in a handler. Every one of these calls CreatorStatusService, which
// is the only caller of CreatorStatusPolicy — so a status cannot be decided one way on a read and
// another on a write.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// The caller's own creator projection.
/// <para>
/// <b>A query that writes, declared as such.</b> Reading the dashboard refreshes the status and
/// opens a creator record for a member who has never had one, which is a side effect a GET should
/// own up to — <c>[SideEffecting]</c> is how DSP4001 lets it. The alternative, a nightly backfill,
/// would leave a window in which a real member had no creator identity and every creator route
/// refused them.
/// </para>
/// </summary>
[Authorize(Permission = CreatorPermissions.ProfileRead)]
[SideEffecting(Reason =
    "Opens the caller's creator record on first read and brings their status up to date. A member "
    + "opening the creator dashboard should find a projection rather than a 404.")]
public sealed record GetMyCreatorProfileQuery : IQuery<CreatorProfileResponse>;

public sealed class GetMyCreatorProfileQueryHandler
    : IQueryHandler<GetMyCreatorProfileQuery, CreatorProfileResponse>
{
    private readonly CreatorContext _context;
    private readonly CreatorStatusService _status;
    private readonly IUnitOfWork _unitOfWork;

    public GetMyCreatorProfileQueryHandler(
        CreatorContext context, CreatorStatusService status, IUnitOfWork unitOfWork)
    {
        _context = context;
        _status = status;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreatorProfileResponse>> Handle(
        GetMyCreatorProfileQuery query, CancellationToken cancellationToken)
    {
        var creator = await _context.RequireCreatorAsync(cancellationToken);

        if (creator.IsFailure)
        {
            return Result.Failure<CreatorProfileResponse>(creator.Error);
        }

        await _status.RefreshAsync(creator.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Community membership is B12's, and this module takes no reference on it. The field is on
        // the contract because the client's type has it; it is empty here and documented as a B12
        // read the Creator Platform deliberately does not perform.
        return Result.Success(creator.Value.ToResponse([]));
    }
}

/// <summary>
/// The member's own edit.
/// <para>
/// <b>A restricted creator is refused here rather than in the client.</b> §28 of the phase brief:
/// "Do not rely on frontend role checks." The refusal is on the write, names the standing, and does
/// not depend on the member having been shown a disabled button.
/// </para>
/// </summary>
[Authorize(Permission = CreatorPermissions.ProfileManage)]
[Auditable(Action = "creator.profile.update")]
public sealed record UpdateCreatorProfileCommand : ICommand<CreatorProfileResponse>
{
    public required UpdateCreatorProfileRequest Request { get; init; }
}

public sealed class UpdateCreatorProfileCommandValidator
    : AbstractValidator<UpdateCreatorProfileCommand>
{
    public UpdateCreatorProfileCommandValidator()
    {
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.Bio)
            .MaximumLength(CreatorLimits.MaxBioLength)
            .When(command => command.Request?.Bio is not null);

        RuleFor(command => command.Request.Categories!)
            .Must(values => values.Count <= CreatorLimits.MaxCategories)
            .WithMessage($"At most {CreatorLimits.MaxCategories} categories.")
            .When(command => command.Request?.Categories is not null);

        RuleFor(command => command.Request.Expertise!)
            .Must(values => values.Count <= CreatorLimits.MaxExpertise)
            .WithMessage($"At most {CreatorLimits.MaxExpertise} areas of expertise.")
            .When(command => command.Request?.Expertise is not null);

        RuleFor(command => command.Request.Languages!)
            .Must(values => values.Count <= CreatorLimits.MaxLanguages)
            .WithMessage($"At most {CreatorLimits.MaxLanguages} languages.")
            .When(command => command.Request?.Languages is not null);
    }
}

public sealed class UpdateCreatorProfileCommandHandler
    : ICommandHandler<UpdateCreatorProfileCommand, CreatorProfileResponse>
{
    private readonly CreatorContext _context;
    private readonly CreatorStatusService _status;
    private readonly ICreatorProfileRepository _creators;
    private readonly IUnitOfWork _unitOfWork;
    private readonly Platform.Services.Abstractions.IClock _clock;

    public UpdateCreatorProfileCommandHandler(
        CreatorContext context,
        CreatorStatusService status,
        ICreatorProfileRepository creators,
        IUnitOfWork unitOfWork,
        Platform.Services.Abstractions.IClock clock)
    {
        _context = context;
        _status = status;
        _creators = creators;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<CreatorProfileResponse>> Handle(
        UpdateCreatorProfileCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var creator = await _context.RequireCreatorAsync(cancellationToken);

        if (creator.IsFailure)
        {
            return Result.Failure<CreatorProfileResponse>(creator.Error);
        }

        var allowed = CreatorContext.EnsureCanModify(creator.Value);

        if (allowed.IsFailure)
        {
            return Result.Failure<CreatorProfileResponse>(allowed.Error);
        }

        creator.Value.UpdateProfile(
            command.Request.Bio,
            command.Request.Categories,
            command.Request.Expertise,
            command.Request.Languages,
            command.Request.CoverImageUrl,
            command.Request.IsDiscoverable,
            _clock.UtcNow);

        _creators.Update(creator.Value);

        await _status.RefreshAsync(creator.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(creator.Value.ToResponse([]));
    }
}

/// <summary>
/// CREATOR-GAP-001, -014. Any creator's public projection.
/// <para>
/// <b>The visibility rule is the same one the directory applies, and a hidden creator is a 404.</b>
/// The register: "a creator who opted out is <em>absent from the response</em> rather than filtered
/// by the client." Returning the record with the private fields nulled would still confirm that the
/// creator exists, which is what somebody probing the directory wants to know.
/// </para>
/// <para>
/// <b>The caller sees their own record whatever their discoverability.</b> Somebody who turned
/// discovery off has not hidden from themselves.
/// </para>
/// </summary>
[Authorize(Permission = CreatorPermissions.ProfileRead)]
public sealed record GetPublicCreatorQuery : IQuery<PublicCreatorResponse>
{
    public required Guid CreatorId { get; init; }
}

public sealed class GetPublicCreatorQueryHandler
    : IQueryHandler<GetPublicCreatorQuery, PublicCreatorResponse>
{
    private readonly ICreatorProfileRepository _creators;
    private readonly CreatorBadgeReader _badges;
    private readonly CreatorContext _context;

    public GetPublicCreatorQueryHandler(
        ICreatorProfileRepository creators, CreatorBadgeReader badges, CreatorContext context)
    {
        _creators = creators;
        _badges = badges;
        _context = context;
    }

    public async Task<Result<PublicCreatorResponse>> Handle(
        GetPublicCreatorQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var creator = await _creators.GetAsync(CreatorId.From(query.CreatorId), cancellationToken);

        if (creator is null)
        {
            return Result.Failure<PublicCreatorResponse>(CreatorErrors.CreatorNotFound);
        }

        var caller = _context.RequireUser();
        var isSelf = caller.IsSuccess && caller.Value == creator.UserId;

        var visible = isSelf
            || (creator.IsDiscoverable && !creator.IsSuspended && !creator.IsRestricted);

        if (!visible)
        {
            // Deliberately the same error a missing creator produces. See the class summary.
            return Result.Failure<PublicCreatorResponse>(CreatorErrors.CreatorNotFound);
        }

        var badges = await _badges.ForAsync(creator.Id, cancellationToken);

        return Result.Success(creator.ToPublicResponse(badges));
    }
}
