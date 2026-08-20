using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>
/// Replaces an organization's opening hours.
/// <para>
/// <b>An unrecognised day is refused rather than dropped.</b> Silently discarding a row would leave
/// a body believing it had published a Saturday it had not, and somebody travelling to a closed
/// door. <c>PublicHoliday</c> is a day here because in most diasporas it is the one that differs.
/// </para>
/// </summary>
public sealed class SetOperatingHoursCommandHandler
    : ICommandHandler<SetOperatingHoursCommand, OrganizationResponse>
{
    private readonly IOrganizationRepository _organizations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly OrganizationReadContext _read;
    private readonly IGuidGenerator _guids;

    public SetOperatingHoursCommandHandler(
        IOrganizationRepository organizations,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        OrganizationReadContext read,
        IGuidGenerator guids)
    {
        _organizations = organizations;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _read = read;
        _guids = guids;
    }

    public async Task<Result<OrganizationResponse>> Handle(
        SetOperatingHoursCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var organization = await _organizations.GetAsync(
            OrganizationId.From(command.OrganizationId), cancellationToken);

        if (organization is null)
        {
            return Result.Failure<OrganizationResponse>(
                CommunityLifeErrors.OrganizationNotFound);
        }

        var caller = await _authorization.RequireOrganizationWriteAsync(
            organization, cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<OrganizationResponse>(caller.Error);
        }

        var hours = new List<OrganizationOperatingHour>(command.Request.Hours.Count);

        foreach (var hour in command.Request.Hours)
        {
            if (CommunityLifeSharedMapper.ParseEnum<OperatingDay>(hour.Day) is not { } day)
            {
                return Result.Failure<OrganizationResponse>(
                    CommunityLifeErrors.OperatingHoursInvalid);
            }

            hours.Add(OrganizationOperatingHour.Create(
                _guids.NewSequentialGuid(),
                day,
                CommunityLifeSharedMapper.FromWireTime(hour.OpensAt),
                CommunityLifeSharedMapper.FromWireTime(hour.ClosesAt),
                hour.IsClosed,
                hour.Note));
        }

        var applied = organization.SetOperatingHours(hours, command.Request.BaseRevision);

        if (applied.IsFailure)
        {
            return Result.Failure<OrganizationResponse>(applied.Error);
        }

        _organizations.Update(organization);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await _read.ToResponseAsync(organization, cancellationToken));
    }
}
