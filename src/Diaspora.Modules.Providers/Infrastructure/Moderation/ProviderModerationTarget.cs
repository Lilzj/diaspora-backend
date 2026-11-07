using Diaspora.Contracts.Moderation.V1;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Infrastructure.Moderation;

/// <summary>
/// A provider, as a moderation case sees it (7 Nov 2026).
/// <para>
/// <b>The only way staff change a member's provider now.</b> The owner's routes refuse staff, and the
/// staff route stage 2 added (<c>POST /providers/moderation/{providerId}</c>) is gone: a decision is
/// made on the Trust case and carried out here, in the same transaction, with the real moderator
/// recorded. Providers are reported through Community, as a <c>Listing</c>; Community sends those
/// reports to Trust as content type <c>provider</c>, so this module keeps no report table.
/// </para>
/// <para>
/// <b>A provider has one moderation state, Suspended,</b> which only staff lift and which no owner
/// transition leaves. A hide pending review and a removal both use it; the case knows which of the
/// two it is. The moderator's note is the provider's <c>statusReason</c>, which its team is shown,
/// as the stage 2 route stored it. Neither archives nor closes the provider: the Community hook this
/// replaces archived it on a rejection, which its owner could undo by publishing again, and which
/// retired its paid placements.
/// </para>
/// </summary>
public sealed class ProviderModerationTarget : IModerationTarget
{
    private readonly IProviderRepository _providers;
    private readonly IClock _clock;

    public ProviderModerationTarget(IProviderRepository providers, IClock clock)
    {
        _providers = providers;
        _clock = clock;
    }

    public string ContentType => ModerationContentTypes.Provider;

    public string Area => ModerationAreas.Providers;

    public async Task<ModerationTargetSnapshot?> DescribeAsync(
        Guid contentId, CancellationToken cancellationToken = default)
    {
        if (contentId == Guid.Empty)
        {
            return null;
        }

        var provider = await _providers.GetForReadAsync(ProviderId.From(contentId), cancellationToken);

        // The provider's primary owner is who answers for it, and who is told.
        return provider is null
            ? null
            : new ModerationTargetSnapshot(
                provider.Id.Value, provider.PrimaryOwnerId.Value, provider.Name.Value, provider.Status.ToString());
    }

    public async Task<Result<ModerationTargetChange>> ApplyAsync(
        Guid contentId,
        ModerationOutcome outcome,
        ModerationActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        var provider = await _providers.GetAsync(ProviderId.From(contentId), cancellationToken);

        if (provider is null)
        {
            return Result.Failure<ModerationTargetChange>(ProviderErrors.NotFound);
        }

        var before = provider.Status;
        var now = _clock.UtcNow;
        UserId? moderator = actor.ModeratorUserId is { } id ? UserId.From(id) : null;

        var applied = outcome switch
        {
            // Already suspended: a hide from another case changes nothing, and keeps the reason the
            // owner was given for the suspension that stands.
            ModerationOutcome.HidePendingReview => before == ProviderStatus.Suspended
                ? Result.Success()
                : provider.Suspend(now, actor.Note, moderator),

            // A confirmed removal records its own note as the reason, over the proposal's hide.
            ModerationOutcome.Remove => provider.Suspend(now, actor.Note, moderator),

            // The only way out of a suspension. The note travels on the status-changed event.
            ModerationOutcome.Restore => provider.Reinstate(now, moderator, actor.Note),

            // A provider has no automatic flag to clear.
            ModerationOutcome.NoAction => Result.Success(),

            _ => Result.Failure(ProviderErrors.EnumUnknown)
        };

        return applied.IsFailure
            ? Result.Failure<ModerationTargetChange>(applied.Error)
            : Result.Success(new ModerationTargetChange(before.ToString(), provider.Status.ToString()));
    }
}
