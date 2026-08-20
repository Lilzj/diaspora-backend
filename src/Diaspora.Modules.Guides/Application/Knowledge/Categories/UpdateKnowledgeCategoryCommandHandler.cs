using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Mapping;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.Categories;

/// <summary>
/// Rewrites a navigation row.
/// <para>
/// <b>The revision is checked before anything is mutated.</b> Curation is the one part of this
/// platform where simultaneous editors are the norm — a small team, all at once, during the week a
/// new country launches — and the aggregate check is what turns "your colleague saved first" into a
/// conflict a UI can offer a reload for, rather than a silent overwrite of their work.
/// </para>
/// </summary>
public sealed class UpdateKnowledgeCategoryCommandHandler
    : ICommandHandler<UpdateKnowledgeCategoryCommand, KnowledgeCategoryResponse>
{
    private readonly IKnowledgeCategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly KnowledgeAuthorizationService _authorization;
    private readonly IKnowledgeCache _cache;

    public UpdateKnowledgeCategoryCommandHandler(
        IKnowledgeCategoryRepository categories,
        IUnitOfWork unitOfWork,
        KnowledgeAuthorizationService authorization,
        IKnowledgeCache cache)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _cache = cache;
    }

    public async Task<Result<KnowledgeCategoryResponse>> Handle(
        UpdateKnowledgeCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await _categories.GetByKeyAsync(command.Key, cancellationToken);

        if (category is null)
        {
            return Result.Failure<KnowledgeCategoryResponse>(KnowledgeErrors.CategoryNotFound);
        }

        var request = command.Request;

        var revision = category.EnsureRevision(request.Revision);

        if (revision.IsFailure)
        {
            return Result.Failure<KnowledgeCategoryResponse>(revision.Error);
        }

        var caller = _authorization.CallerId;

        var updated = category.Update(
            request.Name, request.Description, request.Icon, request.Order, caller);

        if (updated.IsFailure)
        {
            return Result.Failure<KnowledgeCategoryResponse>(updated.Error);
        }

        var sources = category.SetSources(request.SourceCategoryKeys, caller);

        if (sources.IsFailure)
        {
            return Result.Failure<KnowledgeCategoryResponse>(sources.Error);
        }

        var providers = category.SetProviderCategories(request.ProviderCategoryKeys, caller);

        if (providers.IsFailure)
        {
            return Result.Failure<KnowledgeCategoryResponse>(providers.Error);
        }

        var localizations = category.SetLocalizations(
            [.. request.Localizations.Select(
                localization =>
                    ((string?)localization.Locale,
                        (string?)localization.Name,
                        localization.Description))],
            caller);

        if (localizations.IsFailure)
        {
            return Result.Failure<KnowledgeCategoryResponse>(localizations.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Wider than it looks, and correct: a renamed row changes the label on every country page
        // that lists it, and there is no cheap way to know which those are.
        await _cache.InvalidateCatalogueAsync(cancellationToken);

        return Result.Success(KnowledgeCategoryMapper.ToCuratorResponse(category));
    }
}
