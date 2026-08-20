using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Knowledge.Curation;

/// <summary>
/// Removes a guide from a shelf.
/// <para>
/// <b>No guide lookup at all, deliberately.</b> The commonest reason to unfeature something is that
/// the guide was withdrawn — which is exactly when resolving it would fail. A curator clearing a
/// dead row off a country page must not be blocked by the row being dead.
/// </para>
/// </summary>
public sealed class UnfeatureKnowledgeGuideCommandHandler
    : ICommandHandler<UnfeatureKnowledgeGuideCommand>
{
    private readonly ICountryKnowledgeDirectoryRepository _directories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly KnowledgeAuthorizationService _authorization;
    private readonly IKnowledgeCache _cache;

    public UnfeatureKnowledgeGuideCommandHandler(
        ICountryKnowledgeDirectoryRepository directories,
        IUnitOfWork unitOfWork,
        KnowledgeAuthorizationService authorization,
        IKnowledgeCache cache)
    {
        _directories = directories;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _cache = cache;
    }

    public async Task<Result> Handle(
        UnfeatureKnowledgeGuideCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        var directory = await _directories.GetAsync(command.CountryCode, cancellationToken);

        if (directory is null)
        {
            return Result.Failure(KnowledgeErrors.DirectoryNotFound);
        }

        var removed = directory.UnfeatureGuide(
            GuideId.From(command.GuideId), command.KnowledgeCategoryKey, caller.Value);

        if (removed.IsFailure)
        {
            return removed;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.InvalidateCountryAsync(directory.CountryCode, cancellationToken);

        return Result.Success();
    }
}
