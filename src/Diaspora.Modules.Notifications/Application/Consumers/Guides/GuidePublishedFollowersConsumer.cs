using Diaspora.Contracts.Community.V1.Follows;
using Diaspora.Contracts.Guides.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Guides;

/// <summary>
/// A member published a new guide; tell the people who follow them.
/// <para>
/// <b>Why it exists (20 Oct 2026).</b> Following a contributor did nothing a member could notice:
/// the follow was stored and counted, and nobody was ever told when the person they followed wrote
/// something. This is the first thing a follow delivers.
/// </para>
/// <para>
/// <b>First publication only.</b> Republishing an edited guide is a revision, not news, and
/// announcing every correction would teach followers to mute the author.
/// </para>
/// <para>
/// <b>The audience comes from Community through <see cref="IMemberFollowerDirectory"/></b>, the one
/// contract in Diaspora.Contracts that answers who follows a member — this module references no
/// business module. Muted follows are already excluded there. Paged, oldest follow first, so a
/// follower gained mid-walk is neither skipped nor told twice.
/// </para>
/// <para>
/// <b>The author's name is asked of Identity</b>, through <see cref="IMemberProfileService"/>,
/// because the published contract carries only the identifier. When nobody can answer, nothing is
/// sent: "Somebody published a guide" is not worth interrupting anybody for, and an unanswered
/// question is not a name.
/// </para>
/// </summary>
public sealed class GuidePublishedFollowersConsumer
    : ModuleEventConsumer<GuidePublishedIntegrationEvent>
{
    /// <summary>Followers read per round trip.</summary>
    private const int PageSize = 200;

    /// <summary>
    /// The most followers one publication reaches. Beyond this an announcement is a campaign, which
    /// is a different feature; the bound keeps one consumed event from running unbounded.
    /// </summary>
    private const int AudienceLimit = 10_000;

    private readonly IMemberFollowerDirectory _followers;
    private readonly IMemberProfileService _members;

    public GuidePublishedFollowersConsumer(
        INotificationDispatcher dispatcher,
        IMemberFollowerDirectory followers,
        IMemberProfileService members)
        : base(dispatcher)
    {
        _followers = followers;
        _members = members;
    }

    protected override string SourceModule => "Guides";

    /// <summary>Nothing without an audience. See <see cref="HandleAsync"/>.</summary>
    protected override IEnumerable<NotificationTrigger> Translate(
        GuidePublishedIntegrationEvent integrationEvent) => [];

    protected override async Task HandleAsync(
        GuidePublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (!integrationEvent.IsFirstPublication || integrationEvent.AuthorId == Guid.Empty)
        {
            return;
        }

        var authorName = await AuthorNameAsync(integrationEvent.AuthorId, cancellationToken);

        if (string.IsNullOrWhiteSpace(authorName))
        {
            return;
        }

        var values = Values();
        values["authorname"] = authorName;
        values["title"] = integrationEvent.Title;
        values["guideid"] = integrationEvent.GuideId.ToString("D");

        for (var skip = 0; skip < AudienceLimit; skip += PageSize)
        {
            var page = await _followers.ListFollowerPageAsync(
                integrationEvent.AuthorId, skip, PageSize, cancellationToken);

            foreach (var follower in page)
            {
                if (follower == Guid.Empty || follower == integrationEvent.AuthorId)
                {
                    continue;
                }

                await DispatchOneAsync(
                    Trigger(
                        UserId.From(follower),
                        "follower",
                        integrationEvent.PublishedAt,
                        values,
                        subjectType: "guide",
                        subjectId: integrationEvent.GuideId.ToString("D")),
                    cancellationToken);
            }

            if (page.Count < PageSize)
            {
                break;
            }
        }
    }

    private async Task<string?> AuthorNameAsync(Guid authorId, CancellationToken cancellationToken)
    {
        if (!_members.IsAvailable)
        {
            return null;
        }

        var profiles = await _members.ResolveAsync([UserId.From(authorId)], cancellationToken);

        return profiles.TryGetValue(authorId, out var profile) ? profile.DisplayName : null;
    }
}
