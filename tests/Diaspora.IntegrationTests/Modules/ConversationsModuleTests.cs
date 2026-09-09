using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// B13.2 driven over real HTTP, through the same host every other module uses.
/// <para>
/// The point of this suite is that the shapes the React Native client already binds come back
/// unchanged. Field names are asserted against the client's own <c>src/types/conversation.ts</c>,
/// not against the backend's DTOs — a rename on either side has to fail here rather than in a
/// mobile build.
/// </para>
/// </summary>
public sealed class ConversationsModuleTests : IdentityTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ConversationsModuleTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Two_members_can_open_a_conversation_and_exchange_messages()
    {
        var (buyer, seller) = await TwoMembersAsync();

        Api.Authenticate(buyer.Token);

        var created = await OpenConversationAsync(seller.UserId, "listing-1", "Toyota Corolla 2018");
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var conversation = await ReadDataAsync<ConversationShape>(created);

        conversation.ConversationId.Should().NotBeEmpty();
        conversation.State.Should().Be("Active");
        conversation.Context.Type.Should().Be("MarketplaceListing");
        conversation.Context.EntityTitle.Should().Be("Toyota Corolla 2018");
        conversation.Participants.Should().HaveCount(2);
        conversation.CanSend.Should().BeTrue();

        conversation.Participants.Should().Contain(participant =>
            participant.UserId == buyer.UserId && participant.IsCurrentUser);

        var sent = await HttpClient.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.ConversationId}/messages",
            new { text = "Is it still available?", localId = "local-1" },
            JsonOptions);

        sent.StatusCode.Should().Be(HttpStatusCode.Created);

        var message = await ReadDataAsync<MessageShape>(sent);

        message.Text.Should().Be("Is it still available?");
        message.Type.Should().Be("text");
        message.LocalId.Should().Be("local-1", "the client replaces its optimistic bubble with this");
        message.Delivery.Status.Should().Be("sent");
        message.SenderId.Should().Be(buyer.UserId);

        // The seller sees it, unread.
        Api.Authenticate(seller.Token);

        var inbox = await ReadDataAsync<ConversationPageShape>(
            await HttpClient.GetAsync("/api/v1/conversations"));

        inbox.Items.Should().ContainSingle();
        inbox.Items[0].UnreadCount.Should().Be(1);
        inbox.Items[0].LastMessage!.Text.Should().Be("Is it still available?");
        inbox.TotalUnread.Should().Be(1);
    }

    /// <summary>
    /// The rule the client's single entry point depends on: one thread per listing per pair.
    /// </summary>
    [Fact]
    public async Task Opening_the_same_conversation_twice_returns_the_same_thread()
    {
        var (buyer, seller) = await TwoMembersAsync();

        Api.Authenticate(buyer.Token);

        var first = await ReadDataAsync<ConversationShape>(
            await OpenConversationAsync(seller.UserId, "listing-dedup", "A bicycle"));

        var second = await ReadDataAsync<ConversationShape>(
            await OpenConversationAsync(seller.UserId, "listing-dedup", "A bicycle"));

        second.ConversationId.Should().Be(first.ConversationId);
    }

    [Fact]
    public async Task Reading_clears_the_unread_count_for_the_reader_only()
    {
        var (buyer, seller) = await TwoMembersAsync();

        Api.Authenticate(buyer.Token);

        var conversation = await ReadDataAsync<ConversationShape>(
            await OpenConversationAsync(seller.UserId, "listing-read", "A desk"));

        await HttpClient.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.ConversationId}/messages",
            new { text = "Hello" },
            JsonOptions);

        Api.Authenticate(seller.Token);

        (await HttpClient.PostAsJsonAsync(
                $"/api/v1/conversations/{conversation.ConversationId}/read",
                new { },
                JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var unread = await ReadDataAsync<UnreadShape>(
            await HttpClient.GetAsync("/api/v1/conversations/unread-count"));

        unread.Total.Should().Be(0);
    }

    /// <summary>
    /// The privacy rule this module is built around: a non-participant is told the conversation does
    /// not exist, in the same words and with the same status as somebody who invented an identifier.
    /// </summary>
    [Fact]
    public async Task A_non_participant_gets_404_rather_than_403()
    {
        var (buyer, seller) = await TwoMembersAsync();
        var outsider = await RegisterMemberAsync();

        Api.Authenticate(buyer.Token);

        var conversation = await ReadDataAsync<ConversationShape>(
            await OpenConversationAsync(seller.UserId, "listing-private", "Private"));

        Api.Authenticate(outsider.Token);

        var real = await HttpClient.GetAsync(
            $"/api/v1/conversations/{conversation.ConversationId}");

        var invented = await HttpClient.GetAsync($"/api/v1/conversations/{Guid.NewGuid()}");

        real.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "403 would confirm the conversation exists, which is the private fact itself");

        invented.StatusCode.Should().Be(real.StatusCode);
    }

    [Fact]
    public async Task A_sender_can_edit_and_delete_their_own_message_and_nobody_else_can()
    {
        var (buyer, seller) = await TwoMembersAsync();

        Api.Authenticate(buyer.Token);

        var conversation = await ReadDataAsync<ConversationShape>(
            await OpenConversationAsync(seller.UserId, "listing-edit", "A lamp"));

        var message = await ReadDataAsync<MessageShape>(
            await HttpClient.PostAsJsonAsync(
                $"/api/v1/conversations/{conversation.ConversationId}/messages",
                new { text = "origianl" },
                JsonOptions));

        Api.Authenticate(seller.Token);

        (await HttpClient.PutAsJsonAsync(
                $"/api/v1/conversations/{conversation.ConversationId}/messages/{message.MessageId}",
                new { text = "not mine" },
                JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        Api.Authenticate(buyer.Token);

        var edited = await ReadDataAsync<MessageShape>(
            await HttpClient.PutAsJsonAsync(
                $"/api/v1/conversations/{conversation.ConversationId}/messages/{message.MessageId}",
                new { text = "original" },
                JsonOptions));

        edited.Text.Should().Be("original");
        edited.IsEdited.Should().BeTrue();

        var deleted = await ReadDataAsync<MessageShape>(
            await HttpClient.DeleteAsync(
                $"/api/v1/conversations/{conversation.ConversationId}/messages/{message.MessageId}"));

        deleted.IsDeleted.Should().BeTrue();
        deleted.Text.Should().BeNull("the row survives; what it said does not");

        // The tombstone is still in history, because a reply quoting it has to render.
        var detail = await ReadDataAsync<ConversationDetailShape>(
            await HttpClient.GetAsync($"/api/v1/conversations/{conversation.ConversationId}"));

        detail.Messages.Should().Contain(row => row.MessageId == message.MessageId);
    }

    [Fact]
    public async Task Archiving_muting_and_pinning_are_per_participant()
    {
        var (buyer, seller) = await TwoMembersAsync();

        Api.Authenticate(buyer.Token);

        var conversation = await ReadDataAsync<ConversationShape>(
            await OpenConversationAsync(seller.UserId, "listing-flags", "A chair"));

        var id = conversation.ConversationId;

        (await HttpClient.PutAsync($"/api/v1/conversations/{id}/archive", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await HttpClient.PutAsJsonAsync(
                $"/api/v1/conversations/{id}/mute", new { duration = "24h" }, JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await HttpClient.PutAsJsonAsync(
                $"/api/v1/conversations/{id}/pin", new { }, JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var archived = await ReadDataAsync<ConversationPageShape>(
            await HttpClient.GetAsync("/api/v1/conversations?filter=Archived"));

        archived.Items.Should().ContainSingle().Which.IsArchived.Should().BeTrue();
        archived.Items[0].IsMuted.Should().BeTrue();
        archived.Items[0].MutedUntil.Should().NotBeNull("a 24h mute has an end");

        // The other party is unaffected — this is the case a single stored state field cannot hold.
        Api.Authenticate(seller.Token);

        var sellerInbox = await ReadDataAsync<ConversationPageShape>(
            await HttpClient.GetAsync("/api/v1/conversations"));

        sellerInbox.Items.Should().ContainSingle();
        sellerInbox.Items[0].IsArchived.Should().BeFalse();
        sellerInbox.Items[0].IsMuted.Should().BeFalse();
        sellerInbox.Items[0].State.Should().Be("Active");
    }

    [Fact]
    public async Task Unarchiving_and_unmuting_go_through_the_routes_the_client_calls()
    {
        var (buyer, seller) = await TwoMembersAsync();

        Api.Authenticate(buyer.Token);

        var id = (await ReadDataAsync<ConversationShape>(
            await OpenConversationAsync(seller.UserId, "listing-undo", "A rug"))).ConversationId;

        await HttpClient.PutAsync($"/api/v1/conversations/{id}/archive", null);
        await HttpClient.PutAsync($"/api/v1/conversations/{id}/unarchive", null);

        await HttpClient.PutAsJsonAsync(
            $"/api/v1/conversations/{id}/mute", new { duration = "8h" }, JsonOptions);

        // `duration: null` is the client's unmute. Refusing it would leave no way to unmute.
        await HttpClient.PutAsJsonAsync(
            $"/api/v1/conversations/{id}/mute", new { duration = (string?)null }, JsonOptions);

        await HttpClient.PutAsJsonAsync(
            $"/api/v1/conversations/{id}/pin", new { unpin = true }, JsonOptions);

        var inbox = await ReadDataAsync<ConversationPageShape>(
            await HttpClient.GetAsync("/api/v1/conversations"));

        var row = inbox.Items.Should().ContainSingle().Subject;

        row.IsArchived.Should().BeFalse();
        row.IsMuted.Should().BeFalse();
        row.IsPinned.Should().BeFalse();
        row.State.Should().Be("Active");
    }

    /// <summary>
    /// Blocking stops new messages in both directions and leaves the thread readable.
    /// </summary>
    [Fact]
    public async Task Blocking_stops_messages_in_both_directions()
    {
        var (buyer, seller) = await TwoMembersAsync();

        Api.Authenticate(buyer.Token);

        var id = (await ReadDataAsync<ConversationShape>(
            await OpenConversationAsync(seller.UserId, "listing-block", "A tent"))).ConversationId;

        (await HttpClient.PostAsJsonAsync(
                "/api/v1/conversations/block",
                new { userId = seller.UserId, reason = "spam" },
                JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await HttpClient.PostAsJsonAsync(
                $"/api/v1/conversations/{id}/messages", new { text = "still there?" }, JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // The blocked party cannot write either, and is not told why.
        Api.Authenticate(seller.Token);

        (await HttpClient.PostAsJsonAsync(
                $"/api/v1/conversations/{id}/messages", new { text = "hello?" }, JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Reading still works. History does not disappear because somebody blocked somebody.
        (await HttpClient.GetAsync($"/api/v1/conversations/{id}")).StatusCode
            .Should().Be(HttpStatusCode.OK);

        Api.Authenticate(buyer.Token);

        var blocked = await ReadDataAsync<List<BlockedUserShape>>(
            await HttpClient.GetAsync("/api/v1/conversations/blocked"));

        blocked.Should().ContainSingle().Which.UserId.Should().Be(seller.UserId);
        blocked[0].Reason.Should().Be("spam");

        (await HttpClient.DeleteAsync($"/api/v1/conversations/blocked/{seller.UserId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await HttpClient.PostAsJsonAsync(
                $"/api/v1/conversations/{id}/messages", new { text = "back" }, JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// The client sends its own <c>localId</c> so a retry after a timeout is one message, not two.
    /// </summary>
    [Fact]
    public async Task Resending_with_the_same_local_id_returns_the_same_message()
    {
        var (buyer, seller) = await TwoMembersAsync();

        Api.Authenticate(buyer.Token);

        var id = (await ReadDataAsync<ConversationShape>(
            await OpenConversationAsync(seller.UserId, "listing-retry", "A kettle"))).ConversationId;

        var body = new { text = "sent once", localId = "local-retry-1" };

        var first = await ReadDataAsync<MessageShape>(
            await HttpClient.PostAsJsonAsync(
                $"/api/v1/conversations/{id}/messages", body, JsonOptions));

        var second = await ReadDataAsync<MessageShape>(
            await HttpClient.PostAsJsonAsync(
                $"/api/v1/conversations/{id}/messages", body, JsonOptions));

        second.MessageId.Should().Be(first.MessageId);

        var detail = await ReadDataAsync<ConversationDetailShape>(
            await HttpClient.GetAsync($"/api/v1/conversations/{id}"));

        detail.Messages.Count(message => message.Text == "sent once").Should().Be(1);
    }

    [Fact]
    public async Task An_unknown_context_type_is_refused()
    {
        var (buyer, seller) = await TwoMembersAsync();

        Api.Authenticate(buyer.Token);

        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/conversations",
            new
            {
                recipientId = seller.UserId,
                context = new { type = "NotAThing", entityId = "x", entityTitle = "x" }
            },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// A system-managed type must not be openable by a client, and is refused as though it did not
    /// exist — telling a caller it exists but is off-limits is telling them what to probe for.
    /// </summary>
    [Fact]
    public async Task A_system_managed_context_type_cannot_be_opened_by_a_client()
    {
        var (buyer, seller) = await TwoMembersAsync();

        Api.Authenticate(buyer.Token);

        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/conversations",
            new
            {
                recipientId = seller.UserId,
                context = new { type = "SupportTicket", entityId = "t-1", entityTitle = "Help" }
            },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_context_type_catalogue_is_seeded_and_served()
    {
        var member = await RegisterMemberAsync();
        Api.Authenticate(member.Token);

        var types = await ReadDataAsync<List<ContextTypeShape>>(
            await HttpClient.GetAsync("/api/v1/conversations/context-types"));

        types.Should().HaveCountGreaterThanOrEqualTo(12);
        types.Should().Contain(type => type.Key == "MarketplaceListing");
        types.Should().Contain(type => type.Key == "HousingListing");
        types.Should().Contain(type => type.Key == "JobOpportunity");

        types.Should().Contain(type =>
            type.Key == "PrivateConversation" && type.RequiresAcceptance);
    }

    /// <summary>
    /// A stranger opening a private conversation produces a request rather than a thread.
    /// </summary>
    [Fact]
    public async Task A_private_conversation_starts_as_a_request_the_recipient_must_accept()
    {
        var (initiator, recipient) = await TwoMembersAsync();

        Api.Authenticate(initiator.Token);

        var created = await ReadDataAsync<ConversationShape>(
            await HttpClient.PostAsJsonAsync(
                "/api/v1/conversations",
                new
                {
                    recipientId = recipient.UserId,
                    context = new
                    {
                        type = "PrivateConversation",
                        entityId = recipient.UserId.ToString(),
                        entityTitle = "Direct message"
                    },
                    initialMessage = "Hello, can we talk?"
                },
                JsonOptions));

        created.State.Should().Be(
            "Accepted", "the initiator opened it; it is the recipient who owes an answer");

        Api.Authenticate(recipient.Token);

        var requests = await ReadDataAsync<List<RequestShape>>(
            await HttpClient.GetAsync("/api/v1/conversations/requests"));

        var request = requests.Should().ContainSingle().Subject;

        request.RequestId.Should().Be(created.ConversationId);
        request.PreviewMessage.Should().Be("Hello, can we talk?");
        request.CanAccept.Should().BeTrue();

        var accepted = await ReadDataAsync<ConversationShape>(
            await HttpClient.PostAsync(
                $"/api/v1/conversations/requests/{request.RequestId}/accept", null));

        accepted.State.Should().Be("Active");

        (await ReadDataAsync<List<RequestShape>>(
                await HttpClient.GetAsync("/api/v1/conversations/requests")))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task Declining_a_request_closes_it_without_telling_the_initiator_it_was_declined()
    {
        var (initiator, recipient) = await TwoMembersAsync();

        Api.Authenticate(initiator.Token);

        var created = await ReadDataAsync<ConversationShape>(
            await HttpClient.PostAsJsonAsync(
                "/api/v1/conversations",
                new
                {
                    recipientId = recipient.UserId,
                    context = new
                    {
                        type = "PrivateConversation",
                        entityId = recipient.UserId.ToString(),
                        entityTitle = "Direct message"
                    },
                    initialMessage = "Hi"
                },
                JsonOptions));

        Api.Authenticate(recipient.Token);

        (await HttpClient.PostAsync(
                $"/api/v1/conversations/requests/{created.ConversationId}/decline", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        Api.Authenticate(initiator.Token);

        var detail = await ReadDataAsync<ConversationDetailShape>(
            await HttpClient.GetAsync($"/api/v1/conversations/{created.ConversationId}"));

        detail.Conversation.State.Should().Be("Closed");
        detail.Conversation.CanSend.Should().BeFalse();
    }

    [Fact]
    public async Task Message_history_pages_by_cursor_without_dropping_or_repeating()
    {
        var (buyer, seller) = await TwoMembersAsync();

        Api.Authenticate(buyer.Token);

        var id = (await ReadDataAsync<ConversationShape>(
            await OpenConversationAsync(seller.UserId, "listing-page", "A shelf"))).ConversationId;

        // Sent in one loop, so several share a millisecond — the tie the cursor has to survive.
        for (var index = 0; index < 25; index++)
        {
            await HttpClient.PostAsJsonAsync(
                $"/api/v1/conversations/{id}/messages",
                new { text = $"message {index}" },
                JsonOptions);
        }

        var seen = new List<Guid>();
        string? cursor = null;

        do
        {
            var url = cursor is null
                ? $"/api/v1/conversations/{id}/messages?limit=10"
                : $"/api/v1/conversations/{id}/messages?limit=10&cursor={Uri.EscapeDataString(cursor)}";

            var page = await ReadDataAsync<MessagePageShape>(await HttpClient.GetAsync(url));

            seen.AddRange(page.Items.Select(message => message.MessageId));
            cursor = page.HasMore ? page.NextCursor : null;
        }
        while (cursor is not null);

        seen.Should().HaveCount(25);
        seen.Should().OnlyHaveUniqueItems("a cursor that repeats rows is one that drops them too");
    }

    [Fact]
    public async Task Search_only_reaches_the_callers_own_conversations()
    {
        var (buyer, seller) = await TwoMembersAsync();
        var outsider = await RegisterMemberAsync();

        Api.Authenticate(buyer.Token);

        var id = (await ReadDataAsync<ConversationShape>(
            await OpenConversationAsync(seller.UserId, "listing-search", "A piano"))).ConversationId;

        await HttpClient.PostAsJsonAsync(
            $"/api/v1/conversations/{id}/messages",
            new { text = "the secret word is marzipan" },
            JsonOptions);

        var mine = await ReadDataAsync<SearchShape>(
            await HttpClient.PostAsJsonAsync(
                "/api/v1/conversations/search", new { query = "marzipan" }, JsonOptions));

        mine.Results.Should().ContainSingle();

        Api.Authenticate(outsider.Token);

        var theirs = await ReadDataAsync<SearchShape>(
            await HttpClient.PostAsJsonAsync(
                "/api/v1/conversations/search", new { query = "marzipan" }, JsonOptions));

        theirs.Results.Should().BeEmpty("search is scoped in the query, not filtered afterwards");
    }

    [Fact]
    public async Task Anonymous_callers_are_refused_everywhere()
    {
        Api.ClearAuthentication();

        (await HttpClient.GetAsync("/api/v1/conversations")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);

        (await HttpClient.PostAsJsonAsync(
                "/api/v1/conversations",
                new { recipientId = Guid.NewGuid(), context = new { type = "x", entityId = "y" } },
                JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────

    private async Task<(TestMember Buyer, TestMember Seller)> TwoMembersAsync() =>
        (await RegisterMemberAsync(), await RegisterMemberAsync());

    private async Task<TestMember> RegisterMemberAsync()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        return new TestMember(auth.User.Id, auth.Tokens.AccessToken);
    }

    /// <summary>
    /// Unwraps the platform's { data, meta } envelope, and fails loudly with the body when the
    /// status is not a success — a bare deserialisation failure tells you nothing about why.
    /// </summary>
    private static Task<T> ReadDataAsync<T>(HttpResponseMessage response) =>
        IdentityApiClient.ReadDataAsync<T>(response);

    [Fact]
    public async Task A_conversation_cannot_be_opened_with_something_that_is_not_a_person()
    {
        // **Nothing used to check.** `Conversation.Open` took the identifier on faith, so a
        // conversation could be opened against anything at all — an organization's identifier, a
        // listing's, a typo's — and the platform stored it. Nothing could deliver to it, the
        // other side could never open it, and the client rendered it as "Unknown" above a context
        // card naming the subject. That is what an organization's Message button was producing.
        var (member, _) = await TwoMembersAsync();

        Api.Authenticate(member.Token);

        var response = await OpenConversationAsync(
            Guid.NewGuid(), "listing-not-a-person", "Something nobody can answer");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_other_party_comes_back_with_a_name_on_it()
    {
        // The symptom the refusal above prevents: a counterparty row with nothing in it. The
        // client read `displayName` off this and, finding it empty, said "Unknown".
        var (buyer, seller) = await TwoMembersAsync();

        Api.Authenticate(buyer.Token);

        var created = await OpenConversationAsync(seller.UserId, "listing-named", "A real listing");
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var conversation = await ReadDataAsync<ConversationShape>(created);
        var other = conversation.Participants.Single(participant => !participant.IsCurrentUser);

        other.UserId.Should().Be(seller.UserId);
        other.DisplayName.Should().NotBeNullOrWhiteSpace();
    }

    private Task<HttpResponseMessage> OpenConversationAsync(
        Guid recipientId, string entityId, string title) =>
        HttpClient.PostAsJsonAsync(
            "/api/v1/conversations",
            new
            {
                recipientId,
                context = new
                {
                    type = "MarketplaceListing",
                    entityId,
                    entityTitle = title
                }
            },
            JsonOptions);

    private sealed record TestMember(Guid UserId, string Token);
}

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Wire shapes, spelled as the React Native client spells them.
//
// Deliberately hand-written rather than reusing the module's response records: binding against the
// production type would pass even if the production type were renamed, which is the one failure this
// suite exists to catch.
// ════════════════════════════════════════════════════════════════════════════════════════════════

internal sealed record ConversationShape
{
    public Guid ConversationId { get; init; }

    public string State { get; init; } = string.Empty;

    public ContextShape Context { get; init; } = new();

    public List<ParticipantShape> Participants { get; init; } = [];

    public MessageShape? LastMessage { get; init; }

    public int UnreadCount { get; init; }

    public bool IsPinned { get; init; }

    public bool IsMuted { get; init; }

    public bool IsArchived { get; init; }

    public bool IsBlocked { get; init; }

    public DateTimeOffset? MutedUntil { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public int? PinOrder { get; init; }

    public bool CanSend { get; init; }
}

internal sealed record ContextShape
{
    public string Type { get; init; } = string.Empty;

    public string EntityId { get; init; } = string.Empty;

    public string EntityTitle { get; init; } = string.Empty;

    public string? EntitySubtitle { get; init; }

    public string? EntityImageUrl { get; init; }

    public Guid? EntityImageMediaId { get; init; }

    public string? CurrentEntityStatus { get; init; }

    public DateTimeOffset SnapshotAt { get; init; }
}

internal sealed record ParticipantShape
{
    public Guid UserId { get; init; }

    public string? DisplayName { get; init; }

    public string? AvatarUrl { get; init; }

    public TrustShape Trust { get; init; } = new();

    public bool IsCurrentUser { get; init; }

    public string Role { get; init; } = string.Empty;
}

internal sealed record TrustShape
{
    public bool IsVerified { get; init; }

    public string? VerificationLabel { get; init; }
}

internal sealed record MessageShape
{
    public Guid MessageId { get; init; }

    public Guid ConversationId { get; init; }

    public Guid SenderId { get; init; }

    public string Type { get; init; } = string.Empty;

    public string? Text { get; init; }

    public bool IsEdited { get; init; }

    public bool IsDeleted { get; init; }

    public DeliveryShape Delivery { get; init; } = new();

    public DateTimeOffset CreatedAt { get; init; }

    public string? LocalId { get; init; }
}

internal sealed record DeliveryShape
{
    public string Status { get; init; } = string.Empty;

    public DateTimeOffset? SentAt { get; init; }

    public DateTimeOffset? DeliveredAt { get; init; }

    public DateTimeOffset? ReadAt { get; init; }
}

internal sealed record ConversationPageShape
{
    public List<ConversationShape> Items { get; init; } = [];

    public string? NextCursor { get; init; }

    public bool HasMore { get; init; }

    public int TotalUnread { get; init; }

    public List<string> UnappliedFilters { get; init; } = [];
}

internal sealed record ConversationDetailShape
{
    public ConversationShape Conversation { get; init; } = new();

    public List<MessageShape> Messages { get; init; } = [];

    public string? NextCursor { get; init; }

    public bool HasMore { get; init; }
}

internal sealed record MessagePageShape
{
    public List<MessageShape> Items { get; init; } = [];

    public string? NextCursor { get; init; }

    public bool HasMore { get; init; }
}

internal sealed record RequestShape
{
    public Guid RequestId { get; init; }

    public ParticipantShape FromUser { get; init; } = new();

    public string PreviewMessage { get; init; } = string.Empty;

    public bool CanAccept { get; init; }

    public bool CanDecline { get; init; }
}

internal sealed record BlockedUserShape
{
    public Guid UserId { get; init; }

    public string? DisplayName { get; init; }

    public DateTimeOffset BlockedAt { get; init; }

    public string? Reason { get; init; }
}

internal sealed record ContextTypeShape
{
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public bool RequiresAcceptance { get; init; }

    public bool AllowsAttachments { get; init; }
}

internal sealed record SearchShape
{
    public List<SearchResultShape> Results { get; init; } = [];

    public string Provider { get; init; } = string.Empty;
}

internal sealed record SearchResultShape
{
    public Guid ConversationId { get; init; }

    public string ConversationTitle { get; init; } = string.Empty;

    public string? HighlightedText { get; init; }
}

internal sealed record UnreadShape
{
    public int Total { get; init; }

    public DateTimeOffset At { get; init; }
}
