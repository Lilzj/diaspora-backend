using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Policies;
using Diaspora.Modules.Notifications.Extensibility.Throttling;
using Diaspora.Modules.Notifications.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Notifications.Infrastructure.Throttling;

/// <summary>
/// Throttles per member per channel, on top of the platform's subject rate limiter.
/// <para>
/// <b>Built on <see cref="ISubjectRateLimiter"/> rather than a counter of its own, deliberately.</b>
/// That port already solves the hard part — a sliding window, keyed on the thing being protected
/// rather than on a connection — and it already has a Redis implementation waiting for the day one
/// instance stops being enough. Writing a second limiter here would be a second thing to make
/// distributed later.
/// </para>
/// <para>
/// <b>Critical is never throttled, and the ordering of that check matters.</b> A member being
/// flooded with reactions is also a member who might be under attack, and the security alert is
/// exactly the message that must arrive. The key is namespaced per channel so a burst of push does
/// not silence email.
/// </para>
/// </summary>
public sealed class SubjectRateLimitNotificationThrottle : INotificationThrottle
{
    private readonly ISubjectRateLimiter _limiter;
    private readonly IOptions<NotificationOptions> _options;

    public SubjectRateLimitNotificationThrottle(
        ISubjectRateLimiter limiter, IOptions<NotificationOptions> options)
    {
        _limiter = limiter;
        _options = options;
    }

    public async ValueTask<bool> TryAcquireAsync(
        UserId userId,
        NotificationChannelKind channel,
        NotificationPriority priority,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        if (!NotificationPriorityPolicy.CanBeSuppressedByPreference(priority))
        {
            return true;
        }

        var options = _options.Value;

        // The inbox has no cost to anybody. Throttling it would mean silently dropping messages a
        // member could simply scroll past, which is the one outcome worse than too many of them.
        if (channel == NotificationChannelKind.InApp)
        {
            return true;
        }

        var lease = await _limiter.AcquireAsync(
            $"notifications:{channel}:{userId.Value}",
            options.MaxPerChannelPerWindow,
            TimeSpan.FromMinutes(options.ThrottleWindowMinutes),
            cancellationToken);

        return lease.IsAllowed;
    }
}
