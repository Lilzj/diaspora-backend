using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Mapping;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Queries.GetAccessState;

public sealed class GetAccessStateQueryHandler : IQueryHandler<GetAccessStateQuery, AccessStateResponse>
{
    private readonly IUserRepository _users;
    private readonly IAccessGateway _accessGateway;
    private readonly ICurrentIdentityContext _currentIdentity;
    private readonly IClock _clock;

    public GetAccessStateQueryHandler(
        IUserRepository users,
        IAccessGateway accessGateway,
        ICurrentIdentityContext currentIdentity,
        IClock clock)
    {
        _users = users;
        _accessGateway = accessGateway;
        _currentIdentity = currentIdentity;
        _clock = clock;
    }

    public async Task<Result<AccessStateResponse>> Handle(
        GetAccessStateQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentIdentity.UserId;
        if (userId is null)
        {
            return Result.Failure<AccessStateResponse>(UserErrors.InvalidCredentials);
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<AccessStateResponse>(UserErrors.NotFound);
        }

        // Resolved from the stored aggregate, not from the token's claims. A token issued
        // before a suspension still asserts the old state; this endpoint must not.
        var decision = _accessGateway.Resolve(user, _clock.UtcNow);

        return Result.Success(new AccessStateResponse
        {
            State = user.AccessState.Name,
            AccountStatus = user.AccountStatus.ToString(),
            Decision = AccessDecisionMapper.ToResponse(decision),
            NextStates = user.AccessState.AllowedTransitions.Select(state => state.Name).ToArray()
        });
    }
}
