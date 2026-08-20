using Diaspora.Application.Abstractions;
using Diaspora.Modules.Bookmarks.Contracts;
using Diaspora.Modules.Bookmarks.Domain;
using Diaspora.Modules.Bookmarks.Observability;
using Diaspora.Modules.Bookmarks.Persistence;
using Diaspora.Domain.Abstractions;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Bookmarks.Application;

/// <summary>
/// Saves something for the signed-in member.
/// <para>
/// The handler uses the same <see cref="ICommand{T}"/>, <see cref="Result{T}"/>,
/// <see cref="IUnitOfWork"/> and pipeline the Identity handlers use, and the kernel's
/// <see cref="ICurrentUser"/> for the caller — never an Identity type. That is the whole
/// demonstration: a feature module writes against the platform's application abstractions
/// and nothing module-specific to Identity.
/// </para>
/// </summary>
[Authorize(Permission = BookmarkPermissions.Manage)]
public sealed record CreateBookmarkCommand : ICommand<BookmarkResponse>
{
    public required string TargetType { get; init; }

    public required string TargetId { get; init; }

    public string? Note { get; init; }
}

public sealed class CreateBookmarkCommandValidator : AbstractValidator<CreateBookmarkCommand>
{
    public CreateBookmarkCommandValidator()
    {
        RuleFor(command => command.TargetType)
            .NotEmpty()
            .MaximumLength(Bookmark.MaxTargetTypeLength);

        RuleFor(command => command.TargetId)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.Note)
            .MaximumLength(Bookmark.MaxNoteLength);
    }
}

public sealed class CreateBookmarkCommandHandler : ICommandHandler<CreateBookmarkCommand, BookmarkResponse>
{
    private readonly IBookmarkRepository _bookmarks;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly BookmarkTargetCatalog _targets;

    public CreateBookmarkCommandHandler(
        IBookmarkRepository bookmarks,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        BookmarkTargetCatalog targets)
    {
        _bookmarks = bookmarks;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _targets = targets;
    }

    public async Task<Result<BookmarkResponse>> Handle(
        CreateBookmarkCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return Result.Failure<BookmarkResponse>(BookmarkErrors.NotAuthenticated);
        }

        var normalizedType = request.TargetType.Trim().ToLowerInvariant();
        var normalizedTarget = request.TargetId.Trim();

        // The type must be something a module claims. Existence of the target is deliberately
        // not checked — BookmarkTargetCatalog says why — but a type nothing owns can never
        // resolve, so accepting it would only store a row no client could ever render.
        if (!_targets.IsKnown(normalizedType))
        {
            return Result.Failure<BookmarkResponse>(
                BookmarkErrors.UnknownTargetType(normalizedType, _targets.Known));
        }

        var existing = await _bookmarks.GetAsync(userId, normalizedType, normalizedTarget, cancellationToken);
        if (existing is not null)
        {
            // Saving the same thing twice is idempotent, not an error. Update the note in
            // case it changed and return the row that already exists.
            existing.UpdateNote(request.Note);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(BookmarkMapper.ToResponse(existing));
        }

        var bookmark = Bookmark.Create(userId, normalizedType, normalizedTarget, request.Note);
        await _bookmarks.AddAsync(bookmark, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        BookmarkTelemetrySource.Created.Add(1);

        return Result.Success(BookmarkMapper.ToResponse(bookmark));
    }
}
