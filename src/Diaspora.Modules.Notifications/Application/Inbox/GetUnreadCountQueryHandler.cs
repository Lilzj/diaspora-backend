using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Inboxes;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Inbox;

/// <summary>
/// The badge.
/// <para>
/// A member with no inbox row has never received anything, so zero is the correct answer and
/// creating a row to say so would be a write on a read path taken millions of times a day.
/// </para>
/// </summary>
public sealed class GetUnreadCountQueryHandler : IQueryHandler<GetUnreadCountQuery, UnreadCountResponse>
{
    private readonly IUserInboxRepository _inboxes;
    private readonly NotificationAuthorizationService _authorization;

    public GetUnreadCountQueryHandler(
        IUserInboxRepository inboxes, NotificationAuthorizationService authorization)
    {
        _inboxes = inboxes;
        _authorization = authorization;
    }

    public async Task<Result<UnreadCountResponse>> Handle(
        GetUnreadCountQuery query, CancellationToken cancellationToken)
    {
        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<UnreadCountResponse>(caller.Error);
        }

        var inbox = await _inboxes.FindAsync(caller.Value, cancellationToken);

        if (inbox is null)
        {
            return Result.Success(new UnreadCountResponse
            {
                Total = 0,
                ByCategory = new Dictionary<string, int>(StringComparer.Ordinal),
                HasNew = false
            });
        }

        return Result.Success(new UnreadCountResponse
        {
            Total = inbox.UnreadCount,
            ByCategory = inbox.Categories
                .Where(counter => counter.UnreadCount > 0)
                .ToDictionary(
                    counter => counter.CategoryKey,
                    counter => counter.UnreadCount,
                    StringComparer.Ordinal),

            // "Anything since they last looked" — which a badge cannot express, and which is what a
            // subtle dot on a tab bar needs.
            HasNew = inbox.LastNotificationAt is { } last
                && (inbox.LastViewedAt is not { } viewed || last > viewed),
            LastNotificationAt = inbox.LastNotificationAt
        });
    }
}
