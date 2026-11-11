using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Application.Mapping;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Contracts.V1.Responses;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Trust.Application.Verifications;

/// <summary>The caller's own verification requests.</summary>
[RequirePermission(TrustPermissions.ReadSelf)]
public sealed record ListMyVerificationsQuery : IQuery<PagedResult<VerificationResponse>>
{
    public PaginationRequest Pagination { get; init; } = new();

    public VerificationType? Type { get; init; }

    public VerificationStatus? Status { get; init; }
}

public sealed class ListMyVerificationsQueryHandler
    : IQueryHandler<ListMyVerificationsQuery, PagedResult<VerificationResponse>>
{
    private readonly IVerificationRequestRepository _verifications;
    private readonly ICurrentUser _currentUser;

    public ListMyVerificationsQueryHandler(
        IVerificationRequestRepository verifications, ICurrentUser currentUser)
    {
        _verifications = verifications;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<VerificationResponse>>> Handle(
        ListMyVerificationsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return Result.Failure<PagedResult<VerificationResponse>>(TrustErrors.NotAuthenticated);
        }

        var page = await _verifications.ListForUserAsync(
            userId, request.Pagination.Sanitize(), request.Type, request.Status, cancellationToken);

        return Result.Success(page.Map(verification => verification.ToResponse()));
    }
}

/// <summary>
/// One verification in full.
/// <para>
/// Readable by the member who submitted it and by staff with the review permission. The two
/// see different projections — staff get reviewer identities and internal notes, the member
/// gets neither — which is decided in the mapper rather than by two endpoints.
/// </para>
/// </summary>
[Auditable(Action = "trust.verification.read", OnlyForStaff = true)]
[RequirePermission(TrustPermissions.ReadSelf)]
public sealed record GetVerificationQuery : IQuery<VerificationDetailResponse>
{
    public required Guid VerificationId { get; init; }
}

public sealed class GetVerificationQueryHandler
    : IQueryHandler<GetVerificationQuery, VerificationDetailResponse>
{
    private readonly IVerificationRequestRepository _verifications;
    private readonly ICurrentUser _currentUser;
    private readonly DocumentLinkFactory _links;
    private readonly IMemberProfileService _members;

    public GetVerificationQueryHandler(
        IVerificationRequestRepository verifications,
        ICurrentUser currentUser,
        DocumentLinkFactory links,
        IMemberProfileService members)
    {
        _verifications = verifications;
        _currentUser = currentUser;
        _links = links;
        _members = members;
    }

    public async Task<Result<VerificationDetailResponse>> Handle(
        GetVerificationQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return Result.Failure<VerificationDetailResponse>(TrustErrors.NotAuthenticated);
        }

        var verification = await _verifications.GetAsync(
            VerificationRequestId.From(request.VerificationId), cancellationToken);

        if (verification is null)
        {
            return Result.Failure<VerificationDetailResponse>(TrustErrors.VerificationNotFound);
        }

        var isOwner = verification.UserId == userId;
        var isStaff = _currentUser.Permissions.Contains(TrustPermissions.ReviewVerification);

        if (!isOwner && !isStaff)
        {
            // Not-found rather than forbidden. Confirming that a verification exists for
            // someone else is itself a disclosure.
            return Result.Failure<VerificationDetailResponse>(TrustErrors.VerificationNotFound);
        }

        // Links are minted per request and only for callers entitled to the file. When no
        // storage provider is registered the map comes back empty and every DocumentUrl is
        // null, which the response type already models.
        var urls = await _links.CreateLinksAsync(
            verification.Documents.Select(document => (document.Id.Value, document.StorageKey)),
            cancellationToken);

        var detail = verification.ToDetailResponse(urls, forStaff: isStaff);

        if (!isStaff)
        {
            return Result.Success(detail);
        }

        // What a reviewer is actually comparing: the person, their document and their face.
        // The document links above were always minted; the name and the selfie were not, so the
        // console showed an identifier and a document with nothing to compare it against.
        var members = await _members.ResolveAsync([verification.UserId], cancellationToken);

        return Result.Success(detail with
        {
            MemberName = members.TryGetValue(verification.UserId.Value, out var member)
                ? member.DisplayName
                : null,
            SelfieUrl = verification.SelfieStorageKey is { } selfie
                ? await _links.TryCreateLinkAsync(selfie, cancellationToken)
                : null
        });
    }
}

/// <summary>The staff review queue.</summary>
[RequirePermission(TrustPermissions.ReviewVerification)]
public sealed record ListVerificationQueueQuery : IQuery<PagedResult<VerificationQueueItemResponse>>
{
    public PaginationRequest Pagination { get; init; } = new();

    public VerificationType? Type { get; init; }

    public VerificationStatus? Status { get; init; }

    /// <summary>Only items assigned to this reviewer.</summary>
    public Guid? AssignedTo { get; init; }

    /// <summary>Only items past their SLA.</summary>
    public bool OverdueOnly { get; init; }
}

public sealed class ListVerificationQueueQueryHandler
    : IQueryHandler<ListVerificationQueueQuery, PagedResult<VerificationQueueItemResponse>>
{
    private readonly IVerificationRequestRepository _verifications;
    private readonly ITrustProfileRepository _profiles;
    private readonly IClock _clock;

    public ListVerificationQueueQueryHandler(
        IVerificationRequestRepository verifications,
        ITrustProfileRepository profiles,
        IClock clock)
    {
        _verifications = verifications;
        _profiles = profiles;
        _clock = clock;
    }

    public async Task<Result<PagedResult<VerificationQueueItemResponse>>> Handle(
        ListVerificationQueueQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = _clock.UtcNow;

        var page = await _verifications.ListQueueAsync(
            request.Pagination.Sanitize(),
            request.Type,
            request.Status,
            request.AssignedTo is { } assignee
                ? UserId.From(assignee)
                : null,
            request.OverdueOnly,
            now,
            cancellationToken);

        // Each row carries the submitter's standing so a reviewer can triage without opening
        // every item. Loaded per row rather than joined because the page is at most fifty and
        // the profile read is a keyed lookup.
        var items = new List<VerificationQueueItemResponse>(page.Items.Count);

        foreach (var verification in page.Items)
        {
            var profile = await _profiles.GetByUserReadOnlyAsync(verification.UserId, cancellationToken);

            items.Add(verification.ToQueueItem(
                now,
                profile?.Tier.ToString(),
                profile?.ViolationCount ?? 0));
        }

        return Result.Success(PagedResult<VerificationQueueItemResponse>.Create(
            items, page.Page, page.PageSize, page.TotalCount));
    }
}

/// <summary>Takes a verification onto the caller's desk, or releases it.</summary>
[RequirePermission(TrustPermissions.ReviewVerification)]
[Transactional]
public sealed record AssignVerificationCommand : ICommand<VerificationResponse>
{
    public required Guid VerificationId { get; init; }

    /// <summary>False releases the item back to the queue.</summary>
    public bool Assign { get; init; } = true;
}

public sealed class AssignVerificationCommandValidator : AbstractValidator<AssignVerificationCommand>
{
    public AssignVerificationCommandValidator()
    {
        RuleFor(command => command.VerificationId).NotEmpty();
    }
}

public sealed class AssignVerificationCommandHandler
    : ICommandHandler<AssignVerificationCommand, VerificationResponse>
{
    private readonly IVerificationRequestRepository _verifications;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public AssignVerificationCommandHandler(
        IVerificationRequestRepository verifications,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _verifications = verifications;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<VerificationResponse>> Handle(
        AssignVerificationCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reviewerId = _currentUser.UserId;
        if (reviewerId is null)
        {
            return Result.Failure<VerificationResponse>(TrustErrors.NotAuthenticated);
        }

        var verification = await _verifications.GetAsync(
            VerificationRequestId.From(request.VerificationId), cancellationToken);

        if (verification is null)
        {
            return Result.Failure<VerificationResponse>(TrustErrors.VerificationNotFound);
        }

        if (verification.UserId == reviewerId)
        {
            return Result.Failure<VerificationResponse>(TrustErrors.SelfReviewForbidden);
        }

        try
        {
            if (request.Assign)
            {
                verification.Assign(reviewerId, _clock.UtcNow);
            }
            else
            {
                verification.Release(_clock.UtcNow);
            }
        }
        catch (DomainException exception)
        {
            return Result.Failure<VerificationResponse>(
                Error.Conflict(TrustErrorCodes.VerificationNotPending, exception.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(verification.ToResponse());
    }
}
