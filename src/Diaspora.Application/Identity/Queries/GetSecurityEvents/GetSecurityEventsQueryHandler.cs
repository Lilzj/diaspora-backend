using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Mapping;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Queries.GetSecurityEvents;

public sealed class GetSecurityEventsQueryHandler
    : IQueryHandler<GetSecurityEventsQuery, IReadOnlyList<SecurityEventResponse>>
{
    private const int MaxLimit = 200;

    private readonly ISecurityEventRepository _securityEvents;
    private readonly ICurrentIdentityContext _currentIdentity;

    public GetSecurityEventsQueryHandler(
        ISecurityEventRepository securityEvents,
        ICurrentIdentityContext currentIdentity)
    {
        _securityEvents = securityEvents;
        _currentIdentity = currentIdentity;
    }

    public async Task<Result<IReadOnlyList<SecurityEventResponse>>> Handle(
        GetSecurityEventsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentIdentity.UserId;
        if (userId is null)
        {
            return Result.Failure<IReadOnlyList<SecurityEventResponse>>(UserErrors.InvalidCredentials);
        }

        var limit = Math.Clamp(request.Limit, 1, MaxLimit);
        var events = await _securityEvents.ListByUserAsync(userId, limit, cancellationToken);

        return Result.Success(SecurityEventMapper.ToResponses(events));
    }
}
