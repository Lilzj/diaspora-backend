using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Mapping;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.Categories;

/// <summary>
/// Lifts an archival.
/// <para>
/// Read through <see cref="IKnowledgeCategoryRepository.GetIncludingArchivedAsync"/> — the one
/// member that sees past the shared context's global filter. Every other read is right to hide
/// archived rows; this one has to load the row archiving hid.
/// </para>
/// <para>
/// Restoring is an ordinary state change rather than an un-removal, because there is no such thing
/// as un-removing on a change tracker. That is also what lets the aggregate raise the event a cache
/// invalidation and a contract relay both need.
/// </para>
/// </summary>
public sealed class RestoreKnowledgeCategoryCommandHandler
    : ICommandHandler<RestoreKnowledgeCategoryCommand, KnowledgeCategoryResponse>
{
    private readonly IKnowledgeCategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IKnowledgeCache _cache;

    public RestoreKnowledgeCategoryCommandHandler(
        IKnowledgeCategoryRepository categories,
        IUnitOfWork unitOfWork,
        IKnowledgeCache cache)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<Result<KnowledgeCategoryResponse>> Handle(
        RestoreKnowledgeCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await _categories.GetIncludingArchivedAsync(
            command.Key, cancellationToken);

        if (category is null)
        {
            return Result.Failure<KnowledgeCategoryResponse>(KnowledgeErrors.CategoryNotFound);
        }

        if (!category.IsDeleted)
        {
            return Result.Failure<KnowledgeCategoryResponse>(KnowledgeErrors.CategoryNotArchived);
        }

        category.Restore();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.InvalidateCatalogueAsync(cancellationToken);

        return Result.Success(KnowledgeCategoryMapper.ToCuratorResponse(category));
    }
}
