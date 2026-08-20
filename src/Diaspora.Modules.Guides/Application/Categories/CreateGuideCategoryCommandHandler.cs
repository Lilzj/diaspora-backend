using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Categories;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Categories;

/// <summary>
/// Writes a new catalogue entry.
/// <para>
/// The key check is advisory — the unique index is what actually guarantees uniqueness — but it
/// turns the ordinary case, an administrator retyping a key that already exists, into a clear
/// conflict rather than a constraint violation and a rolled-back transaction.
/// </para>
/// </summary>
public sealed class CreateGuideCategoryCommandHandler
    : ICommandHandler<CreateGuideCategoryCommand, GuideCategoryResponse>
{
    private readonly IGuideCategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreateGuideCategoryCommandHandler(
        IGuideCategoryRepository categories,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<GuideCategoryResponse>> Handle(
        CreateGuideCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;

        if (await _categories.KeyExistsAsync(request.Key, cancellationToken))
        {
            return Result.Failure<GuideCategoryResponse>(GuideErrors.CategoryKeyTaken);
        }

        if (request.ParentKey is not null
            && await _categories.GetByKeyAsync(request.ParentKey, cancellationToken) is null)
        {
            return Result.Failure<GuideCategoryResponse>(GuideErrors.CategoryNotFound);
        }

        var category = GuideCategory.Create(
            GuideCategoryId.From(_guids.NewSequentialGuid()),
            request.Key,
            request.Name,
            _clock.UtcNow,
            request.Description,
            request.Icon,
            request.Order,
            request.ParentKey);

        if (category.IsFailure)
        {
            return Result.Failure<GuideCategoryResponse>(category.Error);
        }

        await _categories.AddAsync(category.Value, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(GuideCategoryMapper.ToResponse(category.Value));
    }
}
