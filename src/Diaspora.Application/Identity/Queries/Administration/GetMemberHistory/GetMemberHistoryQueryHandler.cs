using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Mapping;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Queries.Administration.GetMemberHistory;

public sealed class GetMemberHistoryQueryHandler
    : IQueryHandler<GetMemberHistoryQuery, IReadOnlyList<SecurityEventResponse>>
{
    private const int MaxLimit = 200;

    private readonly ISecurityEventRepository _securityEvents;
    private readonly IUserRepository _users;

    public GetMemberHistoryQueryHandler(
        ISecurityEventRepository securityEvents,
        IUserRepository users)
    {
        _securityEvents = securityEvents;
        _users = users;
    }

    public async Task<Result<IReadOnlyList<SecurityEventResponse>>> Handle(
        GetMemberHistoryQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = UserId.From(request.UserId);

        // Checked first so an identifier nobody holds answers "no such account" rather than
        // an empty history, which reads as "this member has done nothing".
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<IReadOnlyList<SecurityEventResponse>>(UserErrors.NotFound);
        }

        var limit = Math.Clamp(request.Limit, 1, MaxLimit);
        var events = await _securityEvents.ListByUserAsync(userId, limit, cancellationToken);

        return Result.Success(SecurityEventMapper.ToResponses(events));
    }
}
