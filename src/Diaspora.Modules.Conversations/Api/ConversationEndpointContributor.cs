using Diaspora.Modules.Conversations.Application.Blocks;
using Diaspora.Modules.Conversations.Application.Conversations;
using Diaspora.Modules.Conversations.Application.Messages;
using Diaspora.Modules.Conversations.Application.Queries;
using Diaspora.Modules.Conversations.Application.Requests;
using Diaspora.Modules.Conversations.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using Diaspora.Platform.Modules.Contributors;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Conversations.Api;

/// <summary>
/// Every route the client already calls, served.
/// <para>
/// <b>The shapes and the verbs are the client's, not this platform's preferences.</b>
/// <c>PUT /{id}/archive</c> would more conventionally be a PATCH on the conversation; <c>PUT
/// /{id}/pin</c> with <c>{ unpin: true }</c> would more conventionally be a DELETE. Both are what
/// <c>conversation-hub.ts</c> issues today, and changing them would mean a client release before a
/// single conversation could be sent. The one place a REST convention won is where the client had no
/// opinion at all.
/// </para>
/// <para>
/// <b>Route order matters here and is not incidental.</b> <c>/requests</c> and <c>/blocked</c> are
/// mapped before <c>/{conversationId:guid}</c>, and the <c>:guid</c> constraint is what stops the
/// literal segments being swallowed by the parameterised route — the client's own mock handlers had
/// to special-case exactly this, by string-matching sub-paths, and its comment says so.
/// </para>
/// </summary>
public sealed class ConversationEndpointContributor : IEndpointContributor
{
    public string Module => ConversationsModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup(ConversationsModuleMetadata.RoutePrefix)
            .WithTags("Conversations")
            .RequireRateLimiting("diaspora-signin");

        MapInbox(group);
        MapRequests(group);
        MapBlocks(group);
        MapSearch(group);
        MapConversation(group);
        MapMessages(group);
    }

    private static void MapInbox(RouteGroupBuilder group)
    {
        group.MapGet("/", async (
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken,
                string? filter = null,
                string? search = null,
                string? entityId = null,
                string? cursor = null,
                int? limit = null) =>
            {
                var result = await sender.Send(
                    new ListConversationsQuery
                    {
                        Filter = filter,
                        Search = search,
                        EntityId = entityId,
                        Cursor = cursor,
                        Limit = limit
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(ConversationPolicies.Read)
            .WithName("ListConversations")
            .WithSummary("The member's inbox, newest activity first, pinned first.");

        group.MapPost("/", async (
                CreateConversationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new CreateConversationCommand
                    {
                        RecipientId = request.RecipientId,
                        Context = request.Context,
                        InitialMessage = request.InitialMessage
                    },
                    cancellationToken);

                // Created, always — even when an existing conversation came back. The client reads
                // the body and navigates; distinguishing 200 from 201 would be distinguishing "you
                // already had one" from "you did not", which is state it does not act on.
                return PlatformResults.Created(context, result);
            })
            .RequireAuthorization(ConversationPolicies.Participate)
            .WithName("CreateConversation")
            .WithSummary("Find or create a conversation about one entity between two people.");

        group.MapGet("/unread-count", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetConversationUnreadCountQuery(), cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(ConversationPolicies.Read)
            .WithName("GetConversationUnreadCount")
            .WithSummary("The conversation badge, without loading a page of the inbox.");

        group.MapGet("/context-types", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ListConversationContextTypesQuery(), cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(ConversationPolicies.Read)
            .WithName("ListConversationContextTypes")
            .WithSummary("The conversation type catalogue, for the client's filter chips.");
    }

    private static void MapRequests(RouteGroupBuilder group)
    {
        group.MapGet("/requests", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new ListConversationRequestsQuery(), cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(ConversationPolicies.Read)
            .WithName("ListConversationRequests")
            .WithSummary("Message requests waiting on the caller.");

        group.MapPost("/requests/{requestId:guid}/accept", async (
                Guid requestId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new AcceptConversationRequestCommand { RequestId = requestId },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(ConversationPolicies.Participate)
            .WithName("AcceptConversationRequest");

        group.MapPost("/requests/{requestId:guid}/decline", async (
                Guid requestId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new DeclineConversationRequestCommand { RequestId = requestId },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(ConversationPolicies.Participate)
            .WithName("DeclineConversationRequest");
    }

    private static void MapBlocks(RouteGroupBuilder group)
    {
        group.MapGet("/blocked", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new ListBlockedUsersQuery(), cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(ConversationPolicies.Read)
            .WithName("ListBlockedUsers");

        group.MapPost("/block", async (
                BlockUserRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new BlockUserCommand { UserId = request.UserId, Reason = request.Reason },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(ConversationPolicies.Participate)
            .WithName("BlockUser");

        group.MapDelete("/blocked/{userId:guid}", async (
                Guid userId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new UnblockUserCommand { UserId = userId }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(ConversationPolicies.Participate)
            .WithName("UnblockUser");
    }

    private static void MapSearch(RouteGroupBuilder group) =>
        group.MapPost("/search", async (
                SearchConversationsRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new SearchConversationsQuery { Query = request.Query, Limit = request.Limit },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(ConversationPolicies.Read)
            .WithName("SearchConversations")
            .WithSummary("Search inside the caller's own conversations. POST because the query is a body.");

    private static void MapConversation(RouteGroupBuilder group)
    {
        group.MapGet("/{conversationId:guid}", async (
                Guid conversationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken,
                string? cursor = null,
                int? limit = null) =>
            {
                var result = await sender.Send(
                    new GetConversationQuery
                    {
                        ConversationId = conversationId,
                        Cursor = cursor,
                        Limit = limit
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(ConversationPolicies.Read)
            .WithName("GetConversation")
            .WithSummary("One conversation and its first page of history, in one call.");

        group.MapPost("/{conversationId:guid}/read", async (
                Guid conversationId,
                MarkConversationReadRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new MarkConversationReadCommand
                    {
                        ConversationId = conversationId,
                        ThroughMessageId = request?.ThroughMessageId
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(ConversationPolicies.Participate)
            .WithName("MarkConversationRead");

        group.MapPut("/{conversationId:guid}/archive", async (
                Guid conversationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ArchiveConversationCommand
                    {
                        ConversationId = conversationId, Archive = true
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(ConversationPolicies.Participate)
            .WithName("ArchiveConversation");

        group.MapPut("/{conversationId:guid}/unarchive", async (
                Guid conversationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ArchiveConversationCommand
                    {
                        ConversationId = conversationId, Archive = false
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(ConversationPolicies.Participate)
            .WithName("UnarchiveConversation");

        group.MapPut("/{conversationId:guid}/mute", async (
                Guid conversationId,
                MuteConversationRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new MuteConversationCommand
                    {
                        ConversationId = conversationId,

                        // Absent body and `{ duration: null }` are the same thing: an unmute. The
                        // client sends the second from its unmute path.
                        Duration = request?.Duration
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(ConversationPolicies.Participate)
            .WithName("MuteConversation");

        group.MapPut("/{conversationId:guid}/pin", async (
                Guid conversationId,
                PinConversationRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new PinConversationCommand
                    {
                        ConversationId = conversationId, Unpin = request?.Unpin ?? false
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(ConversationPolicies.Participate)
            .WithName("PinConversation");

        group.MapGet("/{conversationId:guid}/media", async (
                Guid conversationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetConversationMediaQuery { ConversationId = conversationId },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(ConversationPolicies.Read)
            .WithName("GetConversationMedia")
            .WithSummary("The attachment gallery: images, documents and voice notes.");
    }

    private static void MapMessages(RouteGroupBuilder group)
    {
        group.MapGet("/{conversationId:guid}/messages", async (
                Guid conversationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken,
                string? cursor = null,
                int? limit = null) =>
            {
                var result = await sender.Send(
                    new ListMessagesQuery
                    {
                        ConversationId = conversationId,
                        Cursor = cursor,
                        Limit = limit
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(ConversationPolicies.Read)
            .WithName("ListConversationMessages");

        group.MapPost("/{conversationId:guid}/messages", async (
                Guid conversationId,
                SendMessageRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new SendMessageCommand
                    {
                        ConversationId = conversationId,
                        Text = request.Text,
                        ReplyToMessageId = request.ReplyToMessageId,
                        LocalId = request.LocalId,
                        Type = request.Type,
                        Attachment = request.Attachment
                    },
                    cancellationToken);

                return PlatformResults.Created(context, result);
            })
            .RequireAuthorization(ConversationPolicies.Participate)
            .WithName("SendConversationMessage");

        group.MapPut("/{conversationId:guid}/messages/{messageId:guid}", async (
                Guid conversationId,
                Guid messageId,
                EditMessageRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new EditMessageCommand
                    {
                        ConversationId = conversationId,
                        MessageId = messageId,
                        Text = request.Text
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(ConversationPolicies.Participate)
            .WithName("EditConversationMessage");

        group.MapDelete("/{conversationId:guid}/messages/{messageId:guid}", async (
                Guid conversationId,
                Guid messageId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new DeleteMessageCommand
                    {
                        ConversationId = conversationId, MessageId = messageId
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(ConversationPolicies.Participate)
            .WithName("DeleteConversationMessage");
    }
}
