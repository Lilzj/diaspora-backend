using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Mapping;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Queries.GetFeatures;

public sealed class GetFeaturesQueryHandler : IQueryHandler<GetFeaturesQuery, FeatureAccessResponse>
{
    private readonly IUserRepository _users;
    private readonly IAccessGateway _accessGateway;
    private readonly ICurrentIdentityContext _currentIdentity;
    private readonly IClock _clock;

    public GetFeaturesQueryHandler(
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

    public async Task<Result<FeatureAccessResponse>> Handle(
        GetFeaturesQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentIdentity.UserId;
        if (userId is null)
        {
            return Result.Failure<FeatureAccessResponse>(UserErrors.InvalidCredentials);
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<FeatureAccessResponse>(UserErrors.NotFound);
        }

        var decision = _accessGateway.Resolve(user, _clock.UtcNow);

        return Result.Success(AccessDecisionMapper.ToFeatureResponse(decision));
    }
}
