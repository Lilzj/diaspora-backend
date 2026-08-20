using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.Modules.Guides.Integration;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Knowledge.Curation;

/// <summary>
/// Features a guide on a country's shelf.
/// <para>
/// <b>Three checks the aggregate cannot make, because it holds identifiers rather than guides.</b>
/// The guide has to exist and be published — featuring a draft would put a dead link on the most-read
/// screen in the product. It has to be about this country — a curator who mistypes a country code
/// would otherwise silently recommend Canadian tax guidance to somebody moving to Mauritius, and
/// that is precisely the failure a knowledge base cannot afford. And the category, if one is named,
/// has to be a row this directory actually lists.
/// </para>
/// <para>
/// <b>Resolved through <c>IGuideDirectory</c>, not the repository.</b> It is the surface B8 publishes
/// for exactly this question, it applies B8's own visibility rules, and it means the directory
/// resolves a guide the same way any other consuming module would.
/// </para>
/// </summary>
public sealed class FeatureKnowledgeGuideCommandHandler
    : ICommandHandler<FeatureKnowledgeGuideCommand>
{
    private readonly ICountryKnowledgeDirectoryRepository _directories;
    private readonly IGuideDirectory _guides;
    private readonly IUnitOfWork _unitOfWork;
    private readonly KnowledgeAuthorizationService _authorization;
    private readonly IKnowledgeCache _cache;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public FeatureKnowledgeGuideCommandHandler(
        ICountryKnowledgeDirectoryRepository directories,
        IGuideDirectory guides,
        IUnitOfWork unitOfWork,
        KnowledgeAuthorizationService authorization,
        IKnowledgeCache cache,
        IGuidGenerator guids,
        IClock clock)
    {
        _directories = directories;
        _guides = guides;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _cache = cache;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result> Handle(
        FeatureKnowledgeGuideCommand command, CancellationToken cancellationToken)
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

        var request = command.Request;

        var revision = directory.EnsureRevision(request.Revision);

        if (revision.IsFailure)
        {
            return revision;
        }

        var guideId = GuideId.From(request.GuideId);

        var guide = await _guides.FindAsync(guideId, cancellationToken);

        if (guide is null)
        {
            return Result.Failure(KnowledgeErrors.GuideNotDiscoverable);
        }

        if (!string.Equals(guide.CountryCode, directory.CountryCode, StringComparison.Ordinal))
        {
            return Result.Failure(KnowledgeErrors.GuideCountryMismatch);
        }

        if (!string.IsNullOrWhiteSpace(request.KnowledgeCategoryKey))
        {
            var key = KnowledgeNormalization.Key(request.KnowledgeCategoryKey);

            if (key is null || !directory.Lists(key))
            {
                return Result.Failure(KnowledgeErrors.CategoryNotFound);
            }
        }

        var featured = directory.FeatureGuide(
            _guids.NewSequentialGuid(),
            guideId,
            request.KnowledgeCategoryKey,
            request.Position,
            request.Note,
            caller.Value,
            _clock.UtcNow);

        if (featured.IsFailure)
        {
            return featured;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.InvalidateCountryAsync(directory.CountryCode, cancellationToken);

        return Result.Success();
    }
}
