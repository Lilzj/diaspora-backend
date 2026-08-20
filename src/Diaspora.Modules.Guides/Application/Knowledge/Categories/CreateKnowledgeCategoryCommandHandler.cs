using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Mapping;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.Categories;

/// <summary>
/// Writes a new navigation row.
/// <para>
/// The key check is advisory — the unique index is what actually guarantees uniqueness — but it
/// turns the ordinary case, an administrator retyping a key that already exists, into a clear
/// conflict rather than a constraint violation and a rolled-back transaction. It checks archived
/// rows too, because an archived key is still taken and re-creating it would strand the archived row
/// beyond any restore.
/// </para>
/// <para>
/// The catalogue cache is dropped on the way out. A new row changes what every country page could
/// list, and nothing else knows to invalidate it.
/// </para>
/// </summary>
public sealed class CreateKnowledgeCategoryCommandHandler
    : ICommandHandler<CreateKnowledgeCategoryCommand, KnowledgeCategoryResponse>
{
    private readonly IKnowledgeCategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly KnowledgeAuthorizationService _authorization;
    private readonly IKnowledgeCache _cache;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreateKnowledgeCategoryCommandHandler(
        IKnowledgeCategoryRepository categories,
        IUnitOfWork unitOfWork,
        KnowledgeAuthorizationService authorization,
        IKnowledgeCache cache,
        IGuidGenerator guids,
        IClock clock)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _cache = cache;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<KnowledgeCategoryResponse>> Handle(
        CreateKnowledgeCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;

        if (await _categories.KeyExistsAsync(request.Key, cancellationToken))
        {
            return Result.Failure<KnowledgeCategoryResponse>(KnowledgeErrors.CategoryKeyTaken);
        }

        var caller = _authorization.CallerId;

        var created = KnowledgeCategory.Create(
            KnowledgeCategoryId.From(_guids.NewSequentialGuid()),
            request.Key,
            request.Name,
            _clock.UtcNow,
            request.Description,
            request.Icon,
            request.Order,
            caller);

        if (created.IsFailure)
        {
            return Result.Failure<KnowledgeCategoryResponse>(created.Error);
        }

        var category = created.Value;

        var configured = Configure(category, request, caller);

        if (configured.IsFailure)
        {
            return Result.Failure<KnowledgeCategoryResponse>(configured.Error);
        }

        await _categories.AddAsync(category, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.InvalidateCatalogueAsync(cancellationToken);

        return Result.Success(KnowledgeCategoryMapper.ToCuratorResponse(category));
    }

    /// <summary>
    /// Applies the collections through the aggregate, stopping at the first refusal.
    /// <para>
    /// Through the aggregate rather than by construction, so a seed and a curator's edit are checked
    /// by exactly the same rules. Stopping at the first refusal rather than collecting them means
    /// the caller gets the specific code — an unusable source key and an unusable locale are
    /// different mistakes with different fixes.
    /// </para>
    /// </summary>
    private static Result Configure(
        KnowledgeCategory category,
        Contracts.V1.Requests.CreateKnowledgeCategoryRequest request,
        SharedKernel.Identity.UserId? caller)
    {
        var sources = category.SetSources(request.SourceCategoryKeys, caller);

        if (sources.IsFailure)
        {
            return sources;
        }

        var providers = category.SetProviderCategories(request.ProviderCategoryKeys, caller);

        if (providers.IsFailure)
        {
            return providers;
        }

        return category.SetLocalizations(
            [.. request.Localizations.Select(
                localization =>
                    ((string?)localization.Locale,
                        (string?)localization.Name,
                        localization.Description))],
            caller);
    }
}
