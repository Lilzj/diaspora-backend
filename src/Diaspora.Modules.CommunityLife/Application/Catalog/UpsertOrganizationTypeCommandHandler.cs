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
/// Adds an organization type or restates one.
/// <para>
/// <b>The key never moves.</b> Every organization filed under it carries it as a plain string, and
/// renaming a key would orphan them all — so the key identifies the row and everything else is
/// presentation that can be corrected freely.
/// </para>
/// <para>
/// <b>Deactivating retires rather than deletes.</b> Bodies already filed under a type keep rendering
/// its name; new ones cannot choose it. Deleting would leave existing pages showing a blank
/// category, which is worse than showing a retired one.
/// </para>
/// </summary>
public sealed class UpsertOrganizationTypeCommandHandler
    : ICommandHandler<UpsertOrganizationTypeCommand, OrganizationTypeResponse>
{
    private readonly IOrganizationTypeRepository _types;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public UpsertOrganizationTypeCommandHandler(
        IOrganizationTypeRepository types,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _types = types;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<OrganizationTypeResponse>> Handle(
        UpsertOrganizationTypeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var staff = _authorization.RequireStaff();

        if (staff.IsFailure)
        {
            return Result.Failure<OrganizationTypeResponse>(staff.Error);
        }

        var now = _clock.UtcNow;

        var existing = await _types.FindByKeyAsync(command.Key, cancellationToken);

        if (existing is null)
        {
            var created = OrganizationType.Create(
                OrganizationTypeId.From(_guids.NewSequentialGuid()),
                command.Key,
                command.Name,
                now,
                command.Description,
                command.IconName,
                command.GroupName,
                command.Order,
                command.RequiresVerification);

            await _types.AddAsync(created, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(CommunityLifeCatalogueMapper.ToResponse(created));
        }

        existing.Describe(
            command.Name,
            command.Description,
            command.IconName,
            command.GroupName,
            command.Order,
            command.RequiresVerification,
            now);

        ApplyActivation(existing, command.IsActive, now);

        _types.Update(existing);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(CommunityLifeCatalogueMapper.ToResponse(existing));
    }

    private static void ApplyActivation(
        OrganizationType type, bool? isActive, DateTimeOffset now)
    {
        if (isActive is not { } wanted || wanted == type.IsActive)
        {
            return;
        }

        if (wanted)
        {
            type.Reinstate(now);
        }
        else
        {
            type.Retire(now);
        }
    }
}
