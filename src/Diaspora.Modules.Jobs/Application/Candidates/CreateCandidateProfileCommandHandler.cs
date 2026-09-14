using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Application.Mapping;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Candidates;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Candidates;

/// <summary>
/// Creates a candidate profile.
/// <para>
/// One per member, enforced here and by a unique index. The profile is created
/// <b>private</b> — the aggregate's default, not a choice this command offers — because somebody
/// job-hunting while employed can be dismissed for being found, and a create form that quietly
/// made them discoverable would have made that decision for them.
/// </para>
/// </summary>
public sealed class CreateCandidateProfileCommandHandler
    : ICommandHandler<CreateCandidateProfileCommand, CandidateProfileResponse>
{
    private readonly ICandidateProfileRepository _candidates;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JobAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreateCandidateProfileCommandHandler(
        ICandidateProfileRepository candidates,
        IUnitOfWork unitOfWork,
        JobAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _candidates = candidates;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<CandidateProfileResponse>> Handle(
        CreateCandidateProfileCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CandidateProfileResponse>(caller.Error);
        }

        if (await _candidates.ExistsForUserAsync(caller.Value, cancellationToken))
        {
            return Result.Failure<CandidateProfileResponse>(JobErrors.CandidateAlreadyExists);
        }

        var headline = CandidateHeadline.Create(
            command.Request.Headline, command.Request.Summary);

        if (headline.IsFailure)
        {
            return Result.Failure<CandidateProfileResponse>(headline.Error);
        }

        var now = _clock.UtcNow;

        var profile = CandidateProfile.Create(
            CandidateProfileId.From(_guids.NewSequentialGuid()),
            caller.Value,
            headline.Value,
            command.Request.CountryCode,
            now,
            command.Request.CityName);

        if (profile.IsFailure)
        {
            return Result.Failure<CandidateProfileResponse>(profile.Error);
        }

        await _candidates.AddAsync(profile.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(CandidateMapper.ToResponse(
            profile.Value, DateOnly.FromDateTime(now.UtcDateTime), forOwner: true));
    }
}
