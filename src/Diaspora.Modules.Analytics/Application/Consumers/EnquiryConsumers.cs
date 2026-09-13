using System.Globalization;
using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Conversations.V1.Events;
using Diaspora.Modules.Analytics.Application.Ingestion;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Policies;

namespace Diaspora.Modules.Analytics.Application.Consumers;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// ENQUIRIES — 13 Sep 2026
//
// An enquiry on this platform is a conversation, owned by Conversations: the "Message" button on a
// listing, a home or a service provider opens one with that item as its context. Until this file,
// the seller, landlord and provider dashboards could not say how many people had got in touch, and
// said instead that nothing recorded it — which was never true.
//
// **Counted at the first message, not when the conversation opens.** Tapping "Message" opens a
// conversation before anything is typed, and two of the first three enquiry conversations in
// production were never written in. Conversations announces the first message on its own contract,
// published whether or not anybody is notified, and later messages are not announced at all.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Records an enquiry when a conversation about a listing, a home or a provider gets its first
/// message.
/// <para>
/// <b>Three context types, each with an entity id the app has been seen to send.</b> The listing
/// screen sends a marketplace listing's id, the home screen a housing listing's, the provider screen
/// a provider's. <c>BusinessAccount</c> is left out although Conversations files it under Providers:
/// nothing in the app opens one, so what its entity id names is a guess this consumer will not make.
/// A job's conversations are an employer and a candidate, usually started by the employer, and are
/// not enquiries.
/// </para>
/// <para>
/// <b>One exception the context type cannot see.</b> A landlord's own page also opens a
/// <c>HousingListing</c> conversation, about the landlord rather than one home, with the landlord's
/// id where a home's would be. No home has that id, so the fact is stored attributed to nobody and
/// appears on no dashboard — the right answer for an enquiry about no particular home.
/// </para>
/// <para>
/// <b>No owner is sent.</b> The item's own publication named its owner by member id, and ingestion
/// asks that dimension — the rule every consumer here follows since the profile-id defect. The same
/// lookup is what excludes an owner writing first about their own listing.
/// </para>
/// <para>
/// <b>One fact per conversation.</b> The conversation id is the deduplication key, so the enquiry is
/// counted once even if its first message reached this module twice.
/// </para>
/// </summary>
public sealed class ConversationFirstMessageAnalyticsConsumer
    : IntegrationEventHandler<ConversationFirstMessageSentIntegrationEvent>
{
    private static readonly Dictionary<string, AnalyticsContentType> EnquiryContexts =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["MarketplaceListing"] = AnalyticsContentType.Marketplace,
            ["HousingListing"] = AnalyticsContentType.Housing,
            ["ProviderInquiry"] = AnalyticsContentType.Provider
        };

    private readonly AnalyticsIngestionService _ingestion;

    public ConversationFirstMessageAnalyticsConsumer(AnalyticsIngestionService ingestion) =>
        _ingestion = ingestion;

    /// <summary>
    /// The kind of content a conversation's context names, or null when a conversation of that
    /// kind is not an enquiry.
    /// </summary>
    public static AnalyticsContentType? ContentTypeFor(string? contextType) =>
        contextType is not null && EnquiryContexts.TryGetValue(contextType, out var contentType)
            ? contentType
            : null;

    protected override async Task HandleAsync(
        ConversationFirstMessageSentIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (ContentTypeFor(integrationEvent.ContextType) is not { } contentType)
        {
            return;
        }

        // Opaque to Conversations and a GUID in all three owning modules. Anything else names no
        // item analytics could attribute, so it is dropped rather than stored against nothing.
        if (!Guid.TryParse(integrationEvent.EntityId, CultureInfo.InvariantCulture, out var contentId)
            || contentId == Guid.Empty)
        {
            return;
        }

        await _ingestion.IngestAsync(
            new AnalyticsFact(
                AnalyticsEventTypes.ContentEnquired,
                Version: 1,
                SourceModule: "Conversations",
                integrationEvent.SentAt,
                ActorUserId: integrationEvent.SenderUserId,
                ContentType: contentType,
                ContentId: contentId,
                OwnerUserId: null,
                SourceEventId: integrationEvent.ConversationId),
            cancellationToken).ConfigureAwait(false);
    }
}
