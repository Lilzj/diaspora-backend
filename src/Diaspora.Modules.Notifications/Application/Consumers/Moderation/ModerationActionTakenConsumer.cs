using System.Globalization;
using Diaspora.Contracts.Moderation.V1;
using Diaspora.Contracts.Moderation.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Moderation;

/// <summary>
/// Tells the owner, and the reporters, about a moderation decision (7 Nov 2026).
/// <para>
/// The owner hears every decision that changes their content: hidden (by a person, or by the system
/// after many serious reports), removed, restored, each with the reason and the note. A decision of
/// no action is not sent to the owner, who may never have known there was a report.
/// </para>
/// <para>
/// Reporters hear "took action" or "found no reason to act" once a person has decided. A hide the
/// system made on its own tells them nothing yet, and a restore answers the owner, not them.
/// </para>
/// </summary>
public sealed class ModerationActionTakenConsumer : ModuleEventConsumer<ModerationActionTakenIntegrationEvent>
{
    // The audiences the moderation rules match on, one per notice.
    public const string OwnerHidden = "owner-hidden";
    public const string OwnerHiddenAutomatically = "owner-hidden-automatically";
    public const string OwnerRemoved = "owner-removed";
    public const string OwnerRestored = "owner-restored";
    public const string Reporter = "reporter";

    public ModerationActionTakenConsumer(INotificationDispatcher dispatcher) : base(dispatcher)
    {
    }

    protected override string SourceModule => "moderation";

    protected override IEnumerable<NotificationTrigger> Translate(
        ModerationActionTakenIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var contentType = Describe(integrationEvent.ContentType);
        var subjectId = integrationEvent.DecisionId.ToString();
        var bySystem = integrationEvent.DecidedByUserId is null;

        var ownerAudience = integrationEvent.Outcome switch
        {
            nameof(ModerationOutcome.HidePendingReview) => bySystem
                ? OwnerHiddenAutomatically
                : OwnerHidden,
            nameof(ModerationOutcome.Remove) => OwnerRemoved,
            nameof(ModerationOutcome.Restore) => OwnerRestored,
            _ => null
        };

        if (ownerAudience is not null && integrationEvent.OwnerUserId != Guid.Empty)
        {
            var values = Values();
            values["contenttype"] = contentType;
            values["title"] = string.IsNullOrWhiteSpace(integrationEvent.ContentTitle)
                ? $"Your {contentType}"
                : integrationEvent.ContentTitle;
            values["reasonlabel"] = integrationEvent.ReasonLabel;
            values["note"] = EndWithStop(integrationEvent.Note);
            values["caseid"] = integrationEvent.CaseId.ToString();
            values["appealdeadline"] = integrationEvent.AppealDeadline is { } deadline
                ? deadline.UtcDateTime.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)
                : string.Empty;

            yield return Trigger(
                UserId.From(integrationEvent.OwnerUserId),
                ownerAudience,
                integrationEvent.TakenAt,
                values,
                subjectType: "moderation-decision",
                subjectId: subjectId);
        }

        if (bySystem)
        {
            yield break;
        }

        var outcomeText = integrationEvent.Outcome switch
        {
            nameof(ModerationOutcome.HidePendingReview) or nameof(ModerationOutcome.Remove) => "took action",
            nameof(ModerationOutcome.NoAction) => "found no reason to act",
            _ => null
        };

        if (outcomeText is null)
        {
            yield break;
        }

        foreach (var reporter in integrationEvent.ReporterUserIds.Distinct())
        {
            if (reporter == Guid.Empty || reporter == integrationEvent.OwnerUserId)
            {
                continue;
            }

            var values = Values();
            values["contenttype"] = contentType;
            values["outcometext"] = outcomeText;

            yield return Trigger(
                UserId.From(reporter),
                Reporter,
                integrationEvent.TakenAt,
                values,
                subjectType: "moderation-decision",
                subjectId: subjectId);
        }
    }

    /// <summary>The content type as a member reads it.</summary>
    private static string Describe(string contentType) => contentType switch
    {
        ModerationContentTypes.Job => "job advert",
        ModerationContentTypes.Provider => "business listing",
        ModerationContentTypes.MarketplaceListing => "listing",
        ModerationContentTypes.HousingListing => "home",
        ModerationContentTypes.Guide => "guide",
        ModerationContentTypes.CommunityPost => "post",
        ModerationContentTypes.CommunityComment => "comment",
        ModerationContentTypes.Event => "event",
        ModerationContentTypes.Organization => "organisation",
        ModerationContentTypes.Fundraiser => "fundraiser",
        ModerationContentTypes.Media => "photo or file",
        _ => "content"
    };

    private static string EndWithStop(string note)
    {
        var trimmed = note?.Trim() ?? string.Empty;

        return trimmed.Length == 0 || trimmed.EndsWith('.') || trimmed.EndsWith('!') || trimmed.EndsWith('?')
            ? trimmed
            : trimmed + ".";
    }
}
