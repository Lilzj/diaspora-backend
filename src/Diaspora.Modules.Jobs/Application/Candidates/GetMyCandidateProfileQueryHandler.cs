using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Application.Mapping;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Candidates;

/// <summary>
/// Reads the caller's own candidate profile.
/// <para>
/// `RequireCandidateAsync` answers <c>jobs.not-candidate</c> when there is none, which is the
/// honest result: a member who has never applied for anything has no profile, and inventing an
/// empty one here would make the client unable to tell "not set up yet" from "set up and blank".
/// </para>
/// </summary>
public sealed class GetMyCandidateProfileQueryHandler
    : IQueryHandler<GetMyCandidateProfileQuery, CandidateProfileResponse>
{
    private readonly JobAuthorizationService _authorization;
    private readonly IClock _clock;

    public GetMyCandidateProfileQueryHandler(JobAuthorizationService authorization, IClock clock)
    {
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<CandidateProfileResponse>> Handle(
        GetMyCandidateProfileQuery query, CancellationToken cancellationToken)
    {
        var candidate = await _authorization.RequireCandidateAsync(cancellationToken);

        if (candidate.IsFailure)
        {
            return Result.Failure<CandidateProfileResponse>(candidate.Error);
        }

        var now = _clock.UtcNow;

        // `forOwner: true` unconditionally — this route only ever serves the caller their own
        // profile, so there is no other projection to choose.
        return Result.Success(CandidateMapper.ToResponse(
            candidate.Value, DateOnly.FromDateTime(now.UtcDateTime), forOwner: true));
    }
}
