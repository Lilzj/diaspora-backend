using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Catalog;
using Diaspora.Modules.Notifications.Domain.Errors;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Modules.Notifications.Domain.Preferences;
using Diaspora.Modules.Notifications.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Notifications.Application.Preferences;

/// <summary>
/// The link in an email footer — B13.3.
/// <para>
/// <b>Deliberately not <c>[Authorize]</c>d, and it is the only write on this surface that is not.</b>
/// An unsubscribe link that required a session would not work for the member it exists for: somebody
/// who has stopped opening the app is exactly the person who wants out. The authority comes from the
/// signature instead, which names one member and one category and can do nothing else — see
/// <see cref="UnsubscribeTokenFactory"/>.
/// </para>
/// </summary>
[Transactional(Reason = "Records the member's decision and the reason for it together.")]
[Auditable]
public sealed record RedeemUnsubscribeTokenCommand : ICommand<UnsubscribeResultResponse>
{
    public required string Token { get; init; }
}

/// <summary>
/// Verifies the signature, switches one category's email off, and says what happened.
/// <para>
/// <b>Idempotent, because it must be.</b> A mail client that prefetches links, a corporate scanner
/// that follows every URL in an inbound message, and a member who clicks twice all arrive here more
/// than once — and the second visit must show the same confirmation rather than an error. The
/// aggregate keeps the first timestamp; this returns success either way.
/// </para>
/// <para>
/// <b>A member with no stored preferences is created one.</b> The common case for somebody who has
/// never opened the settings screen is exactly the member most likely to use a footer link, and
/// refusing because there was no row to update would be refusing the request that matters most.
/// </para>
/// </summary>
public sealed class RedeemUnsubscribeTokenCommandHandler
    : ICommandHandler<RedeemUnsubscribeTokenCommand, UnsubscribeResultResponse>
{
    /// <summary>
    /// Redemptions one member's link may drive in a minute.
    /// <para>
    /// <b>Keyed on the member the token names, not on the caller's address.</b> There is no caller
    /// identity here — that is the point of the endpoint — and an address partition would be shared
    /// by everybody behind one corporate mail gateway, which is exactly the population most likely
    /// to have a scanner following links. Ten is generous for a human pressing a link and low enough
    /// that a loop is stopped.
    /// </para>
    /// <para>
    /// It bounds work, not correctness: the operation is idempotent, so a refused retry costs the
    /// caller nothing they had not already achieved.
    /// </para>
    /// </summary>
    private const int RedemptionsPerMinute = 10;

    private readonly UnsubscribeTokenFactory _tokens;
    private readonly INotificationPreferenceRepository _preferences;
    private readonly INotificationCategoryRepository _categories;
    private readonly ISubjectRateLimiter _rateLimiter;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOptions<NotificationOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public RedeemUnsubscribeTokenCommandHandler(
        UnsubscribeTokenFactory tokens,
        INotificationPreferenceRepository preferences,
        INotificationCategoryRepository categories,
        ISubjectRateLimiter rateLimiter,
        IUnitOfWork unitOfWork,
        IOptions<NotificationOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _tokens = tokens;
        _preferences = preferences;
        _categories = categories;
        _rateLimiter = rateLimiter;
        _unitOfWork = unitOfWork;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<UnsubscribeResultResponse>> Handle(
        RedeemUnsubscribeTokenCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (_tokens.Redeem(command.Token) is not { } claim)
        {
            return Result.Failure<UnsubscribeResultResponse>(
                NotificationErrors.UnsubscribeTokenInvalid);
        }

        // After the signature, deliberately. Limiting before it would let an unauthenticated caller
        // exhaust a member's budget by posting garbage that names them — which is a denial of the
        // one route out of an email they did not want. A forged token costs an HMAC and nothing else.
        var lease = await _rateLimiter.AcquireAsync(
            $"notifications:unsubscribe:{claim.UserId.Value:N}",
            RedemptionsPerMinute,
            TimeSpan.FromMinutes(1),
            cancellationToken);

        if (!lease.IsAllowed)
        {
            return Result.Failure<UnsubscribeResultResponse>(NotificationErrors.RateLimited);
        }

        var category = await _categories.FindByKeyAsync(claim.CategoryKey, cancellationToken);

        // A token naming a category that has since been retired. Reported as an invalid link rather
        // than as a missing category: the holder is not necessarily the member, and "that category
        // no longer exists" is a fact about the platform they have no business learning from an
        // unauthenticated endpoint.
        if (category is null || !category.IsActive)
        {
            return Result.Failure<UnsubscribeResultResponse>(
                NotificationErrors.UnsubscribeTokenInvalid);
        }

        var preference = await _preferences.FindAsync(claim.UserId, cancellationToken);

        if (preference is null)
        {
            preference = NotificationPreference.CreateDefault(
                NotificationPreferenceId.From(_guids.NewSequentialGuid()),
                claim.UserId,
                _clock.UtcNow,
                _options.Value.DefaultLanguage,
                _options.Value.DefaultTimeZoneId);

            await _preferences.AddAsync(preference, cancellationToken);
        }

        var unsubscribed = preference.Unsubscribe(
            category.Key,
            Domain.ValueObjects.CategoryPreference.Sources.EmailLink,
            _clock.UtcNow,
            category.IsMandatory);

        if (unsubscribed.IsFailure)
        {
            return Result.Failure<UnsubscribeResultResponse>(unsubscribed.Error);
        }

        _preferences.Update(preference);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new UnsubscribeResultResponse
        {
            CategoryKey = category.Key,
            DisplayName = category.DisplayName,

            // Named so the confirmation page can tell somebody who followed a link with no session
            // where the rest of their settings live. Without it the page can only say "done", and
            // the member's next move is to look for an unsubscribe link in another email.
            ManageRoute = "/settings/notifications"
        });
    }
}
