using Diaspora.Modules.Notifications.Domain.Digests;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Errors;
using Diaspora.Modules.Notifications.Domain.Notifications;
using Diaspora.Modules.Notifications.Domain.Policies;
using Diaspora.Modules.Notifications.Domain.Preferences;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.Modules.Notifications.Extensibility.Digests;
using Diaspora.Modules.Notifications.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Notifications.Infrastructure.Digests;

/// <summary>
/// Collects a member's digest-bound messages for a window into one summary.
/// <para>
/// <b>Channel-independent, as the brief requires.</b> This produces a list of things that happened.
/// Whether it becomes an email, one push or a card at the top of the inbox is decided afterwards by
/// the same preference logic every other message goes through.
/// </para>
/// <para>
/// <b>Only messages actually marked for a digest are collected.</b> Sweeping up everything in the
/// window would re-summarise messages the member already received individually — which reads as the
/// platform repeating itself, and is exactly the experience a digest exists to prevent.
/// </para>
/// <para>
/// <b>Newest first, capped, and the total is kept.</b> A window holding two hundred reactions shows
/// the first fifty and says there were two hundred, because a digest that reprints everything is the
/// thing it replaces.
/// </para>
/// </summary>
public sealed class DefaultDigestGenerator : IDigestGenerator
{
    private readonly INotificationRepository _notifications;
    private readonly INotificationDigestRepository _digests;
    private readonly INotificationPreferenceRepository _preferences;
    private readonly IOptions<NotificationOptions> _options;
    private readonly IGuidGenerator _guids;

    public DefaultDigestGenerator(
        INotificationRepository notifications,
        INotificationDigestRepository digests,
        INotificationPreferenceRepository preferences,
        IOptions<NotificationOptions> options,
        IGuidGenerator guids)
    {
        _notifications = notifications;
        _digests = digests;
        _preferences = preferences;
        _options = options;
        _guids = guids;
    }

    public async Task<Result<NotificationDigest>> GenerateAsync(
        DigestRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The sweep runs hourly and a schedule fires on an hour; without this a restart inside that
        // hour would generate the same digest twice and send it twice.
        if (await _digests.ExistsForWindowAsync(
                request.RecipientUserId,
                request.Frequency,
                request.WindowStart,
                request.WindowEnd,
                request.CategoryKey,
                cancellationToken))
        {
            return Result.Failure<NotificationDigest>(NotificationErrors.DigestAlreadyGenerated);
        }

        var candidates = await _notifications.ListForDigestAsync(
            request.RecipientUserId,
            request.WindowStart,
            request.WindowEnd,
            request.CategoryKey,
            NotificationLimits.MaxDigestEntries * 4,
            cancellationToken);

        var entries = candidates
            .Where(IsDigestBound)
            .OrderByDescending(notification => notification.CreatedAt)
            .Select(ToEntry)
            .ToList();

        if (entries.Count == 0 && !request.RecordWhenEmpty)
        {
            return Result.Failure<NotificationDigest>(NotificationErrors.DigestEmpty);
        }

        var language = request.Language
                       ?? (await _preferences.FindAsync(request.RecipientUserId, cancellationToken))?.Language
                       ?? _options.Value.DefaultLanguage;

        var digest = NotificationDigest.Assemble(
            Domain.Identifiers.NotificationDigestId.From(_guids.NewSequentialGuid()),
            request.RecipientUserId,
            request.Frequency,
            request.WindowStart,
            request.WindowEnd,
            request.WindowEnd,
            entries,
            language,
            request.CategoryKey,
            request.CountryCode);

        if (digest.IsFailure)
        {
            return digest;
        }

        await _digests.AddAsync(digest.Value, cancellationToken);

        return digest;
    }

    /// <summary>
    /// Whether a message was held for a digest rather than sent on its own.
    /// <para>
    /// Decided from the delivery record, which is where the policy resolver wrote its decision.
    /// Re-deriving it from the priority and the preferences here would be a second implementation of
    /// the same rule, and the two would eventually disagree.
    /// </para>
    /// </summary>
    private static bool IsDigestBound(Notification notification) =>
        notification.Deliveries.Any(delivery =>
            delivery.Status == DeliveryStatus.Digested && delivery.DigestId == Guid.Empty);

    private DigestEntry ToEntry(Notification notification) =>
        DigestEntry.Create(
            _guids.NewSequentialGuid(),
            notification.Id,
            notification.CategoryKey,
            notification.Content.Title,
            notification.Content.PushBody,
            notification.PrimaryLink?.Route,
            notification.CreatedAt);
}
