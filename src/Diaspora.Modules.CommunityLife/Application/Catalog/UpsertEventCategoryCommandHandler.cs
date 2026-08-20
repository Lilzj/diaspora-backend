using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Catalog;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Catalog;

/// <summary>
/// Adds an event category or restates one.
/// <para>
/// <b>The facets are what a filter bar is built from</b>, and they live on the category rather than
/// in their own table so retiring the last category carrying a facet retires the facet with it — a
/// filter offering an option that returns nothing is a filter members stop trusting.
/// </para>
/// <para>
/// <b><c>DefaultsToRsvp</c> is the field that decides whether a new event asks for responses.</b> A
/// Sunday service does not; a workshop with twenty seats does. Putting it here rather than in the
/// client means a category added next year behaves correctly in an app shipped this year.
/// </para>
/// </summary>
public sealed class UpsertEventCategoryCommandHandler
    : ICommandHandler<UpsertEventCategoryCommand, EventCategoryResponse>
{
    private readonly IEventCategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public UpsertEventCategoryCommandHandler(
        IEventCategoryRepository categories,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<EventCategoryResponse>> Handle(
        UpsertEventCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var staff = _authorization.RequireStaff();

        if (staff.IsFailure)
        {
            return Result.Failure<EventCategoryResponse>(staff.Error);
        }

        var now = _clock.UtcNow;

        var existing = await _categories.FindByKeyAsync(command.Key, cancellationToken);

        if (existing is null)
        {
            var created = EventCategory.Create(
                EventCategoryId.From(_guids.NewSequentialGuid()),
                command.Key,
                command.Name,
                now,
                command.Description,
                command.IconName,
                command.GroupName,
                command.Facets,
                command.Order,
                command.DefaultsToRsvp ?? true);

            await _categories.AddAsync(created, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(CommunityLifeCatalogueMapper.ToResponse(created));
        }

        existing.Describe(
            command.Name,
            command.Description,
            command.IconName,
            command.GroupName,
            command.Facets,
            command.Order,
            command.DefaultsToRsvp,
            now);

        ApplyActivation(existing, command.IsActive, now);

        _categories.Update(existing);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(CommunityLifeCatalogueMapper.ToResponse(existing));
    }

    private static void ApplyActivation(
        EventCategory category, bool? isActive, DateTimeOffset now)
    {
        if (isActive is not { } wanted || wanted == category.IsActive)
        {
            return;
        }

        if (wanted)
        {
            category.Reinstate(now);
        }
        else
        {
            category.Retire(now);
        }
    }
}
