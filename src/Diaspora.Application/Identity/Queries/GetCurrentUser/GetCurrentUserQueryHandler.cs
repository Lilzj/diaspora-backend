using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Mapping;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Queries.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler : IQueryHandler<GetCurrentUserQuery, UserProfileResponse>
{
    private readonly IUserRepository _users;
    private readonly ICurrentIdentityContext _currentIdentity;

    public GetCurrentUserQueryHandler(IUserRepository users, ICurrentIdentityContext currentIdentity)
    {
        _users = users;
        _currentIdentity = currentIdentity;
    }

    public async Task<Result<UserProfileResponse>> Handle(
        GetCurrentUserQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentIdentity.UserId;
        if (userId is null)
        {
            return Result.Failure<UserProfileResponse>(UserErrors.InvalidCredentials);
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken);

        return user is null
            ? Result.Failure<UserProfileResponse>(UserErrors.NotFound)
            : Result.Success(UserMapper.ToProfileResponse(user));
    }
}
