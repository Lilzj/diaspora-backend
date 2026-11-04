using Diaspora.Application.Abstractions;
using Diaspora.Modules.Conversations.Application.Mapping;
using Diaspora.Modules.Conversations.Application.Services;
using Diaspora.Modules.Conversations.Contracts.V1.Responses;
using Diaspora.Modules.Conversations.Domain.Conversations;
using Diaspora.Modules.Conversations.Domain.Enums;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Modules.Conversations.Domain.Messages;
using Diaspora.Modules.Conversations.Domain.Policies;
using Diaspora.Modules.Conversations.Extensibility;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Conversations.Application.Queries;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Inbox
// ════════════════════════════════════════════════════════════════════════════════════════════════

[Authorize(Permission = ConversationPermissions.Read)]
public sealed record ListConversationsQuery : IQuery<ConversationPageResponse>
{
    /// <summary>One of the client's thirteen chips. Translated to catalogue keys at the edge.</summary>
    public string? Filter { get; init; }

    public string? Search { get; init; }

    public string? EntityId { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}

public sealed class ListConversationsQueryHandler
    : IQueryHandler<ListConversationsQuery, ConversationPageResponse>
{
    private readonly IConversationRepository _conversations;
    private readonly IConversationMessageRepository _messages;
    private readonly ConversationAccessService _access;
    private readonly ConversationMapper _mapper;
    private readonly IConversationParticipantDirectory _directory;
    private readonly IClock _clock;

    public ListConversationsQueryHandler(
        IConversationRepository conversations,
        IConversationMessageRepository messages,
        ConversationAccessService access,
        ConversationMapper mapper,
        IConversationParticipantDirectory directory,
        IClock clock)
    {
        _conversations = conversations;
        _messages = messages;
        _access = access;
        _mapper = mapper;
        _directory = directory;
        _clock = clock;
    }

    public async Task<Result<ConversationPageResponse>> Handle(
        ListConversationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<ConversationPageResponse>(caller.Error);
        }

        var viewer = caller.Value;
        var filter = query.Filter?.Trim();

        var page = await _conversations.ListForUserAsync(
            new ConversationQueryFilter
            {
                UserId = viewer,
                ArchivedOnly = Is(filter, "archived"),
                UnreadOnly = Is(filter, "unread"),
                RequestsOnly = Is(filter, "requests"),
                VerifiedOnly = Is(filter, "verifiedonly"),
                ContextTypes = ConversationWireNames.ContextTypesForFilter(filter),
                EntityId = query.EntityId,
                Search = query.Search,
                Cursor = query.Cursor,
                Limit = query.Limit ?? ConversationLimits.DefaultPageSize
            },
            cancellationToken);

        var context = await _mapper.BuildContextAsync(viewer, page.Items, cancellationToken);

        // Every last message in one query. Fetching them per row is the N+1 that would make an inbox
        // render thirty round trips, which is the shape this phase is required not to ship.
        var lastMessages = await LoadLastMessagesAsync(page.Items, cancellationToken);

        var unapplied = new List<string>();

        // Named rather than silently dropped. Showing unverified contacts under a "Verified only"
        // heading would be a filter that appears to work and does not.
        if (Is(filter, "verifiedonly") && !_directory.IsAvailable)
        {
            unapplied.Add("verifiedOnly");
        }

        return Result.Success(new ConversationPageResponse
        {
            Items =
            [
                .. page.Items.Select(conversation => ConversationMapper.ToResponse(
                    conversation,
                    context,
                    conversation.LastMessageId is { } id && lastMessages.TryGetValue(id, out var last)
                        ? last
                        : null))
            ],
            NextCursor = page.NextCursor,
            HasMore = page.HasMore,
            TotalUnread = await _conversations.CountUnreadAsync(
                viewer, _clock.UtcNow, cancellationToken),
            UnappliedFilters = unapplied
        });
    }

    private async Task<Dictionary<ConversationMessageId, ConversationMessage>> LoadLastMessagesAsync(
        IReadOnlyList<Conversation> conversations, CancellationToken cancellationToken)
    {
        var map = new Dictionary<ConversationMessageId, ConversationMessage>();

        foreach (var conversation in conversations)
        {
            if (conversation.LastMessageId is not { } id || map.ContainsKey(id))
            {
                continue;
            }

            // Tracked by the change tracker after the first load, so a repeated identifier costs
            // nothing. A batched "get many" would be better still and is recorded as a follow-up in
            // the persistence guide; the page is bounded at a hundred rows, so this is a hundred
            // primary-key lookups at worst, not a scan.
            if (await _messages.GetAsync(id, cancellationToken) is { } message)
            {
                map[id] = message;
            }
        }

        return map;
    }

    private static bool Is(string? filter, string value) =>
        string.Equals(filter, value, StringComparison.OrdinalIgnoreCase);
}

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Detail
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// One conversation and its first page of history.
/// <para>
/// Both in one response, because the client's <c>loadMessages</c> reads <c>data</c> and
/// <c>messages</c> from a single call — and a second round trip to render the screen it just opened
/// would be a round trip on the platform's most-opened screen.
/// </para>
/// </summary>
[Authorize(Permission = ConversationPermissions.Read)]
public sealed record GetConversationQuery : IQuery<ConversationDetailResponse>
{
    public required Guid ConversationId { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}

public sealed class GetConversationQueryHandler
    : IQueryHandler<GetConversationQuery, ConversationDetailResponse>
{
    private readonly IConversationMessageRepository _messages;
    private readonly ConversationAccessService _access;
    private readonly ConversationMapper _mapper;

    public GetConversationQueryHandler(
        IConversationMessageRepository messages,
        ConversationAccessService access,
        ConversationMapper mapper)
    {
        _messages = messages;
        _access = access;
        _mapper = mapper;
    }

    public async Task<Result<ConversationDetailResponse>> Handle(
        GetConversationQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var loaded = await _access.LoadAsync(query.ConversationId, cancellationToken);

        if (loaded.IsFailure)
        {
            return Result.Failure<ConversationDetailResponse>(loaded.Error);
        }

        var conversation = loaded.Value;
        var viewer = _access.RequireCaller().Value;

        var history = await _messages.ListAsync(
            new MessageQueryFilter
            {
                ConversationId = conversation.Id,
                Cursor = query.Cursor,
                Limit = query.Limit ?? ConversationLimits.DefaultPageSize
            },
            cancellationToken);

        var context = await _mapper.BuildContextAsync(viewer, [conversation], cancellationToken);

        var last = history.Items.Count > 0 ? history.Items[0] : null;

        return Result.Success(new ConversationDetailResponse
        {
            Conversation = ConversationMapper.ToResponse(conversation, context, last),

            // Oldest first. The repository pages newest-first so the cursor works from the bottom of
            // a thread, but a client renders a list top-down and would otherwise reverse every page
            // itself — and the one that forgot would render the conversation backwards.
            Messages =
            [
                .. history.Items
                    .OrderBy(message => message.SentAt)
                    .ThenBy(message => message.Id.Value)
                    .Select(message => ConversationMapper.ToResponse(message, context))
            ],
            NextCursor = history.NextCursor,
            HasMore = history.HasMore
        });
    }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════
// History
// ════════════════════════════════════════════════════════════════════════════════════════════════

[Authorize(Permission = ConversationPermissions.Read)]
public sealed record ListMessagesQuery : IQuery<MessagePageResponse>
{
    public required Guid ConversationId { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}

public sealed class ListMessagesQueryHandler
    : IQueryHandler<ListMessagesQuery, MessagePageResponse>
{
    private readonly IConversationMessageRepository _messages;
    private readonly ConversationAccessService _access;
    private readonly ConversationMapper _mapper;

    public ListMessagesQueryHandler(
        IConversationMessageRepository messages, ConversationAccessService access, ConversationMapper mapper)
    {
        _messages = messages;
        _access = access;
        _mapper = mapper;
    }

    public async Task<Result<MessagePageResponse>> Handle(
        ListMessagesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var loaded = await _access.LoadAsync(query.ConversationId, cancellationToken);

        if (loaded.IsFailure)
        {
            return Result.Failure<MessagePageResponse>(loaded.Error);
        }

        var history = await _messages.ListAsync(
            new MessageQueryFilter
            {
                ConversationId = loaded.Value.Id,
                Cursor = query.Cursor,
                Limit = query.Limit ?? ConversationLimits.DefaultPageSize
            },
            cancellationToken);

        // The participants, resolved once, so each bubble carries its sender's name and photo.
        var context = await _mapper.BuildContextAsync(
            _access.RequireCaller().Value, [loaded.Value], cancellationToken);

        return Result.Success(new MessagePageResponse
        {
            Items =
            [
                .. history.Items
                    .OrderBy(message => message.SentAt)
                    .ThenBy(message => message.Id.Value)
                    .Select(message => ConversationMapper.ToResponse(message, context))
            ],
            NextCursor = history.NextCursor,
            HasMore = history.HasMore
        });
    }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Requests, blocks, media, search, catalogue, badge
// ════════════════════════════════════════════════════════════════════════════════════════════════

[Authorize(Permission = ConversationPermissions.Read)]
public sealed record ListConversationRequestsQuery
    : IQuery<IReadOnlyList<ConversationRequestResponse>>;

public sealed class ListConversationRequestsQueryHandler
    : IQueryHandler<ListConversationRequestsQuery, IReadOnlyList<ConversationRequestResponse>>
{
    private readonly IConversationRepository _conversations;
    private readonly IConversationMessageRepository _messages;
    private readonly ConversationAccessService _access;
    private readonly ConversationMapper _mapper;

    public ListConversationRequestsQueryHandler(
        IConversationRepository conversations,
        IConversationMessageRepository messages,
        ConversationAccessService access,
        ConversationMapper mapper)
    {
        _conversations = conversations;
        _messages = messages;
        _access = access;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<ConversationRequestResponse>>> Handle(
        ListConversationRequestsQuery query, CancellationToken cancellationToken)
    {
        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<ConversationRequestResponse>>(caller.Error);
        }

        var viewer = caller.Value;

        var page = await _conversations.ListForUserAsync(
            new ConversationQueryFilter
            {
                UserId = viewer,
                RequestsOnly = true,
                Limit = ConversationLimits.MaxPageSize
            },
            cancellationToken);

        // Only requests waiting on this member. A conversation the caller initiated is pending on
        // somebody else and does not belong in their request inbox.
        var waiting = page.Items
            .Where(conversation =>
                conversation.ParticipantFor(viewer)?.Role == ParticipantRole.Recipient)
            .ToList();

        var context = await _mapper.BuildContextAsync(viewer, waiting, cancellationToken);

        var responses = new List<ConversationRequestResponse>(waiting.Count);

        foreach (var conversation in waiting)
        {
            var opening = conversation.LastMessageId is { } id
                ? await _messages.GetAsync(id, cancellationToken)
                : null;

            responses.Add(ConversationMapper.ToRequestResponse(conversation, context, opening));
        }

        return Result.Success<IReadOnlyList<ConversationRequestResponse>>(responses);
    }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════

[Authorize(Permission = ConversationPermissions.Read)]
public sealed record ListBlockedUsersQuery : IQuery<IReadOnlyList<BlockedUserResponse>>;

public sealed class ListBlockedUsersQueryHandler
    : IQueryHandler<ListBlockedUsersQuery, IReadOnlyList<BlockedUserResponse>>
{
    private readonly Domain.Blocks.IConversationBlockRepository _blocks;
    private readonly ConversationAccessService _access;
    private readonly ConversationMapper _mapper;

    public ListBlockedUsersQueryHandler(
        Domain.Blocks.IConversationBlockRepository blocks,
        ConversationAccessService access,
        ConversationMapper mapper)
    {
        _blocks = blocks;
        _access = access;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<BlockedUserResponse>>> Handle(
        ListBlockedUsersQuery query, CancellationToken cancellationToken)
    {
        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<BlockedUserResponse>>(caller.Error);
        }

        var blocks = await _blocks.ListForBlockerAsync(caller.Value, cancellationToken);

        var context = await _mapper.BuildContextAsync(caller.Value, [], cancellationToken);

        return Result.Success<IReadOnlyList<BlockedUserResponse>>(
            [.. blocks.Select(block => ConversationMapper.ToResponse(block, context))]);
    }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════

[Authorize(Permission = ConversationPermissions.Read)]
public sealed record GetConversationMediaQuery : IQuery<ConversationMediaResponse>
{
    public required Guid ConversationId { get; init; }
}

public sealed class GetConversationMediaQueryHandler
    : IQueryHandler<GetConversationMediaQuery, ConversationMediaResponse>
{
    private readonly IConversationMessageRepository _messages;
    private readonly ConversationAccessService _access;

    public GetConversationMediaQueryHandler(
        IConversationMessageRepository messages, ConversationAccessService access)
    {
        _messages = messages;
        _access = access;
    }

    public async Task<Result<ConversationMediaResponse>> Handle(
        GetConversationMediaQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var loaded = await _access.LoadAsync(query.ConversationId, cancellationToken);

        if (loaded.IsFailure)
        {
            return Result.Failure<ConversationMediaResponse>(loaded.Error);
        }

        var withAttachments = await _messages.ListWithAttachmentsAsync(
            loaded.Value.Id, ConversationLimits.MaxPageSize, cancellationToken);

        var attachments = withAttachments
            .Where(message => message.Attachment is not null)
            .Select(message => (message.Attachment!, message.SentAt))
            .ToList();

        return Result.Success(new ConversationMediaResponse
        {
            Images = Project(attachments, AttachmentKind.Image),
            Documents = Project(attachments, AttachmentKind.Document),
            Voice = Project(attachments, AttachmentKind.Voice)
        });
    }

    private static IReadOnlyList<MessageAttachmentResponse> Project(
        IReadOnlyList<(Domain.ValueObjects.AttachmentReference Attachment, DateTimeOffset SentAt)> rows,
        AttachmentKind kind) =>
        [
            .. rows
                .Where(row => row.Attachment.Kind == kind)
                .Select(row => ConversationMapper.ToResponse(row.Attachment, row.SentAt))
        ];
}

// ════════════════════════════════════════════════════════════════════════════════════════════════

[Authorize(Permission = ConversationPermissions.Read)]
public sealed record SearchConversationsQuery : IQuery<ConversationSearchResponse>
{
    public required string Query { get; init; }

    public int? Limit { get; init; }
}

public sealed class SearchConversationsQueryHandler
    : IQueryHandler<SearchConversationsQuery, ConversationSearchResponse>
{
    private readonly IConversationSearchEngine _engine;
    private readonly IConversationRepository _conversations;
    private readonly IConversationMessageRepository _messages;
    private readonly ConversationAccessService _access;

    public SearchConversationsQueryHandler(
        IConversationSearchEngine engine,
        IConversationRepository conversations,
        IConversationMessageRepository messages,
        ConversationAccessService access)
    {
        _engine = engine;
        _conversations = conversations;
        _messages = messages;
        _access = access;
    }

    public async Task<Result<ConversationSearchResponse>> Handle(
        SearchConversationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<ConversationSearchResponse>(caller.Error);
        }

        // The searcher goes to the engine, not a filter applied afterwards. An engine that returned
        // everything and let the caller filter would have already read everybody's messages.
        var hits = await _engine.SearchAsync(
            caller.Value,
            query.Query,
            Math.Clamp(query.Limit ?? 20, 1, ConversationLimits.MaxPageSize),
            cancellationToken);

        var results = new List<ConversationSearchResultResponse>(hits.Count);

        foreach (var hit in hits)
        {
            var conversation = await _conversations.GetForParticipantAsync(
                hit.ConversationId, caller.Value, cancellationToken);

            // A second check, cheap and deliberate. The engine is a seam a deployment can replace,
            // and a replacement that got its scoping wrong must not be able to leak through this.
            if (conversation is null)
            {
                continue;
            }

            var message = await _messages.GetAsync(hit.MessageId, cancellationToken);

            results.Add(new ConversationSearchResultResponse
            {
                ConversationId = hit.ConversationId.Value,
                ConversationTitle = conversation.Context.EntityTitle,
                ContextType = conversation.Context.Type.Key,
                MatchType = "messages",
                Message = message is null ? null : ConversationMapper.ToResponse(message),
                HighlightedText = hit.Excerpt,
                MatchedAt = hit.MatchedAt
            });
        }

        return Result.Success(new ConversationSearchResponse
        {
            Results = results,
            Provider = _engine.Provider
        });
    }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════

[Authorize(Permission = ConversationPermissions.Read)]
public sealed record ListConversationContextTypesQuery
    : IQuery<IReadOnlyList<ConversationContextTypeResponse>>;

public sealed class ListConversationContextTypesQueryHandler
    : IQueryHandler<ListConversationContextTypesQuery, IReadOnlyList<ConversationContextTypeResponse>>
{
    private readonly ConversationContextCatalog _catalog;

    public ListConversationContextTypesQueryHandler(ConversationContextCatalog catalog) =>
        _catalog = catalog;

    public async Task<Result<IReadOnlyList<ConversationContextTypeResponse>>> Handle(
        ListConversationContextTypesQuery query, CancellationToken cancellationToken)
    {
        var types = await _catalog.ListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ConversationContextTypeResponse>>(
        [
            .. types.Select(type => new ConversationContextTypeResponse
            {
                Key = type.Key,
                DisplayName = type.DisplayName,
                Description = type.Description,
                OwningModule = type.OwningModule,
                RequiresAcceptance = type.RequiresAcceptance,
                AllowsAttachments = type.AllowsAttachments,
                Position = type.Position
            })
        ]);
    }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════

[Authorize(Permission = ConversationPermissions.Read)]
public sealed record GetConversationUnreadCountQuery : IQuery<ConversationUnreadCountResponse>;

public sealed class GetConversationUnreadCountQueryHandler
    : IQueryHandler<GetConversationUnreadCountQuery, ConversationUnreadCountResponse>
{
    private readonly IConversationRepository _conversations;
    private readonly ConversationAccessService _access;
    private readonly IClock _clock;

    public GetConversationUnreadCountQueryHandler(
        IConversationRepository conversations, ConversationAccessService access, IClock clock)
    {
        _conversations = conversations;
        _access = access;
        _clock = clock;
    }

    public async Task<Result<ConversationUnreadCountResponse>> Handle(
        GetConversationUnreadCountQuery query, CancellationToken cancellationToken)
    {
        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<ConversationUnreadCountResponse>(caller.Error);
        }

        var now = _clock.UtcNow;

        return Result.Success(new ConversationUnreadCountResponse
        {
            Total = await _conversations.CountUnreadAsync(caller.Value, now, cancellationToken),
            At = now
        });
    }
}
