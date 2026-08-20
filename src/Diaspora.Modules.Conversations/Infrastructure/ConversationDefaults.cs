using Diaspora.Modules.Conversations.Domain.Conversations;
using Diaspora.Modules.Conversations.Domain.Enums;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Modules.Conversations.Domain.ValueObjects;
using Diaspora.Modules.Conversations.Extensibility;
using Diaspora.Modules.Conversations.Options;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Conversations.Infrastructure;

/// <summary>
/// The shipped participant directory: identifiers, and nothing else.
/// <para>
/// <b>A complete answer, not a stub.</b> This module holds no display names and must not — that is
/// B13's boundary, enforced by an architecture test, and acquiring an Identity reference to render
/// an avatar would undo it. A deployment that wants names registers its own directory after this
/// module and the response shape does not change, which is the point of the seam.
/// </para>
/// </summary>
public sealed class IdentifierOnlyParticipantDirectory : IConversationParticipantDirectory
{
    public bool IsAvailable => false;

    public Task<IReadOnlyDictionary<Guid, ConversationParticipantProfile>> ResolveAsync(
        IReadOnlyCollection<UserId> userIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        IReadOnlyDictionary<Guid, ConversationParticipantProfile> profiles = userIds
            .Select(userId => userId.Value)
            .Distinct()
            .ToDictionary(id => id, ConversationParticipantProfile.IdentifierOnly);

        return Task.FromResult(profiles);
    }
}

/// <summary>
/// The shipped context resolver: says nothing about anything.
/// <para>
/// Live entity status belongs to whichever module owns the entity. Reaching for it from here would
/// mean referencing Marketplace, Housing, Jobs, Providers and Community Life to render one line of
/// an inbox — and would duplicate data with an owner, which this module refuses.
/// </para>
/// </summary>
public sealed class UnavailableContextResolver : IConversationContextResolver
{
    public bool IsAvailable => false;

    public Task<IReadOnlyDictionary<string, ConversationContextStatus>> ResolveAsync(
        IReadOnlyCollection<ConversationReference> references,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, ConversationContextStatus>>(
            new Dictionary<string, ConversationContextStatus>(StringComparer.Ordinal));
}

/// <summary>
/// The shipped attachment rule: the feature flag and the context type's own permission.
/// <para>
/// It asserts nothing about the file. Media (B5) owns the asset and has already scanned, typed and
/// sized it; a second opinion formed here would be formed without the bytes.
/// </para>
/// </summary>
public sealed class DefaultAttachmentValidator : IConversationAttachmentValidator
{
    private readonly IOptionsMonitor<ConversationOptions> _options;

    public DefaultAttachmentValidator(IOptionsMonitor<ConversationOptions> options) =>
        _options = options;

    public Task<ConversationPolicyDecision> ValidateAsync(
        Conversation conversation,
        AttachmentReference attachment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentNullException.ThrowIfNull(attachment);

        return Task.FromResult(_options.CurrentValue.EnableAttachments
            ? ConversationPolicyDecision.Allow()
            : ConversationPolicyDecision.Refuse("Attachments are switched off on this deployment."));
    }
}

/// <summary>
/// The shipped search engine: a LIKE over the caller's own messages.
/// <para>
/// <b>Scoped to conversations the searcher is in, in the query rather than afterwards.</b> Filtering
/// results after the fact would mean the database read everybody's messages to decide the caller
/// could not see them, and the one time somebody removed the filter it would leak silently.
/// </para>
/// <para>
/// Correct, and it gets slow somewhere in the tens of thousands of messages. That is the documented
/// ceiling: a deployment that outgrows it registers an index behind the port. Indexing conversations
/// into a shared search cluster is explicitly out of scope for this phase.
/// </para>
/// </summary>
public sealed class EfConversationSearchEngine : IConversationSearchEngine
{
    private readonly DiasporaDbContext _dbContext;

    public EfConversationSearchEngine(DiasporaDbContext dbContext) => _dbContext = dbContext;

    public string Provider => "ef";

    public async Task<IReadOnlyList<ConversationSearchHit>> SearchAsync(
        UserId searcher, string query, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(searcher);

        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var term = query.Trim();

        var visible = _dbContext.Set<Conversation>()
            .Where(conversation => conversation.Participants.Any(participant =>
                participant.UserId == searcher && participant.LeftAt == null))
            .Select(conversation => conversation.Id);

        var hits = await _dbContext.Set<Domain.Messages.ConversationMessage>()
            .Where(message =>
                visible.Contains(message.ConversationId)
                && message.Status != MessageStatus.Deleted
                && message.Status != MessageStatus.Hidden
                && message.Body != null
                && EF.Functions.Like(message.Body, $"%{term}%"))
            .OrderByDescending(message => message.SentAt)
            .Take(limit)
            .Select(message => new
            {
                message.ConversationId,
                MessageId = message.Id,
                message.SentAt,
                message.Body
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. hits.Select(hit => new ConversationSearchHit
            {
                ConversationId = hit.ConversationId,
                MessageId = hit.MessageId,
                MatchedAt = hit.SentAt,
                Excerpt = Excerpt(hit.Body, term)
            })
        ];
    }

    /// <summary>
    /// A window around the match, so the client can highlight without shipping the whole message.
    /// </summary>
    private static string? Excerpt(string? body, string term)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        const int window = 60;

        var index = body.IndexOf(term, StringComparison.OrdinalIgnoreCase);

        if (index < 0)
        {
            return body.Length <= window * 2 ? body : body[..(window * 2)];
        }

        var start = Math.Max(0, index - window);
        var end = Math.Min(body.Length, index + term.Length + window);

        return body[start..end];
    }
}

/// <summary>
/// The shipped conversation type catalogue: the twelve the client already knows.
/// <para>
/// <b>Contributed rather than compiled in.</b> These are seeds, indistinguishable from a seed a
/// future vertical ships from its own assembly — so nothing about them is privileged and adding a
/// thirteenth needs no change here. <c>RequiresAcceptance</c> is the reason the catalogue carries
/// behaviour: a stranger enquiring about your listing is expected, a stranger opening a private
/// conversation with you is exactly what a message request is for.
/// </para>
/// </summary>
public sealed class PlatformConversationContextTypeContributor : IConversationContextTypeContributor
{
    public string Module => ConversationsModuleMetadata.Name;

    public IEnumerable<ConversationContextTypeSeed> GetContextTypes()
    {
        yield return new ConversationContextTypeSeed
        {
            Key = ConversationTypeKeys.MarketplaceListing,
            DisplayName = "Marketplace listing",
            Description = "A buyer and a seller, about one item.",
            OwningModule = "Marketplace",
            Position = 10
        };

        yield return new ConversationContextTypeSeed
        {
            Key = ConversationTypeKeys.HousingListing,
            DisplayName = "Housing listing",
            Description = "A seeker and a landlord, about one property.",
            OwningModule = "Housing",
            Position = 20
        };

        yield return new ConversationContextTypeSeed
        {
            Key = ConversationTypeKeys.JobOpportunity,
            DisplayName = "Job opportunity",
            Description = "An employer and a candidate, about one role.",
            OwningModule = "Jobs",
            Position = 30
        };

        yield return new ConversationContextTypeSeed
        {
            Key = ConversationTypeKeys.ProviderInquiry,
            DisplayName = "Provider enquiry",
            Description = "A member and a service provider.",
            OwningModule = "Providers",
            Position = 40
        };

        yield return new ConversationContextTypeSeed
        {
            Key = ConversationTypeKeys.CommunityEvent,
            DisplayName = "Community event",
            Description = "An organiser and somebody attending.",
            OwningModule = "CommunityLife",
            Position = 50
        };

        yield return new ConversationContextTypeSeed
        {
            Key = ConversationTypeKeys.Organization,
            DisplayName = "Organisation",
            Description = "A member and an organisation.",
            OwningModule = "CommunityLife",
            Position = 60
        };

        yield return new ConversationContextTypeSeed
        {
            Key = ConversationTypeKeys.CommunityGuide,
            DisplayName = "Guide",
            Description = "A reader and a guide's author.",
            OwningModule = "Guides",
            Position = 70
        };

        yield return new ConversationContextTypeSeed
        {
            Key = ConversationTypeKeys.BusinessAccount,
            DisplayName = "Business account",
            Description = "A member and a business.",
            OwningModule = "Providers",
            Position = 80
        };

        // A stranger with no shared context is the case message requests exist for. Everything above
        // carries its own introduction — you enquired about their listing — and needs no gate.
        yield return new ConversationContextTypeSeed
        {
            Key = ConversationTypeKeys.PrivateConversation,
            DisplayName = "Private conversation",
            Description = "Two members, about nothing in particular.",
            RequiresAcceptance = true,
            Position = 90
        };

        // Opened by the platform on a member's behalf. A client that could post one directly could
        // manufacture a thread that looks official.
        yield return new ConversationContextTypeSeed
        {
            Key = ConversationTypeKeys.SupportTicket,
            DisplayName = "Support",
            Description = "A member and the support team.",
            IsSystemManaged = true,
            Position = 100
        };

        yield return new ConversationContextTypeSeed
        {
            Key = ConversationTypeKeys.TrustAppeal,
            DisplayName = "Trust appeal",
            Description = "A member appealing a verification or moderation decision.",
            OwningModule = "Trust",
            IsSystemManaged = true,
            AllowsAttachments = true,
            Position = 110
        };

        // Seeded so the catalogue matches the client's twelve literals, and system-managed so
        // nothing can open one. AI conversations are explicitly out of scope for this phase; the row
        // exists to reserve the key, not to enable a feature.
        yield return new ConversationContextTypeSeed
        {
            Key = ConversationTypeKeys.AiAssistant,
            DisplayName = "Assistant",
            Description = "Reserved. No producer ships in this phase.",
            IsSystemManaged = true,
            AllowsAttachments = false,
            Position = 120
        };
    }
}
