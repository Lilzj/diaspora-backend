using Diaspora.Modules.Conversations.Domain.Blocks;
using Diaspora.Modules.Conversations.Domain.Conversations;
using Diaspora.Modules.Conversations.Domain.Errors;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Conversations.Application.Services;

/// <summary>
/// The one door every handler goes through to reach a conversation.
/// <para>
/// <b>It exists so that "not yours" and "does not exist" can never be told apart, and so that the
/// rule is written once.</b> Twenty-one handlers each loading a conversation and each deciding what
/// to say when the caller is not in it is twenty-one chances for one of them to return 403 — and one
/// 403 is enough to let anybody probe identifiers and learn that a conversation exists between two
/// named people. On a surface where somebody may be talking to an immigration adviser or a landlord,
/// that existence <em>is</em> the private fact.
/// </para>
/// <para>
/// Every method here returns <c>ConversationNotFound</c> for both cases. The domain's
/// <c>NotAParticipant</c> error exists for internal paths and tests and is translated here; an
/// integration test asserts the status code a non-participant sees is 404.
/// </para>
/// </summary>
public sealed class ConversationAccessService
{
    private readonly IConversationRepository _conversations;
    private readonly IConversationBlockRepository _blocks;
    private readonly ICurrentUser _currentUser;

    public ConversationAccessService(
        IConversationRepository conversations,
        IConversationBlockRepository blocks,
        ICurrentUser currentUser)
    {
        _conversations = conversations;
        _blocks = blocks;
        _currentUser = currentUser;
    }

    /// <summary>
    /// The caller, or a failure when the request carries no usable subject.
    /// <para>
    /// Every endpoint here requires authorisation, so a missing subject means the token was accepted
    /// and carried no <c>sub</c> — which is a misconfiguration, not a member's mistake. Returning
    /// Unauthorized rather than throwing keeps it a response the client can act on.
    /// </para>
    /// </summary>
    public Result<UserId> RequireCaller() =>
        _currentUser.UserId is { } userId
            ? Result.Success(userId)
            : Result.Failure<UserId>(Error.Unauthorized(
                ConversationErrorCodes.NotAParticipant, "You are not signed in."));

    /// <summary>Loads a conversation the caller is an active participant in.</summary>
    public async Task<Result<Conversation>> LoadAsync(
        Guid conversationId, CancellationToken cancellationToken)
    {
        var caller = RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<Conversation>(caller.Error);
        }

        if (conversationId == Guid.Empty)
        {
            return Result.Failure<Conversation>(ConversationErrors.ConversationNotFound);
        }

        var conversation = await _conversations.GetForParticipantAsync(
            ConversationId.From(conversationId), caller.Value, cancellationToken);

        return conversation is null
            ? Result.Failure<Conversation>(ConversationErrors.ConversationNotFound)
            : Result.Success(conversation);
    }

    /// <summary>
    /// Loads a conversation and refuses when a block stands between the caller and anybody in it.
    /// <para>
    /// <b>Reading is still permitted; this is for writes.</b> A blocked conversation stays readable —
    /// what was said before the block is exactly what somebody may need to refer to — but nothing
    /// new may be added in either direction. Refusing the read as well would delete history from a
    /// member's view by way of somebody else's decision.
    /// </para>
    /// </summary>
    public async Task<Result<Conversation>> LoadForWriteAsync(
        Guid conversationId, CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(conversationId, cancellationToken);

        if (loaded.IsFailure)
        {
            return loaded;
        }

        var caller = _currentUser.UserId!;

        if (await IsBlockedWithinAsync(loaded.Value, caller, cancellationToken))
        {
            return Result.Failure<Conversation>(ConversationErrors.Blocked);
        }

        return loaded;
    }

    /// <summary>
    /// Whether a block stands between this member and any other active participant.
    /// <para>
    /// Asked in both directions, because a member who was blocked must not be able to keep writing
    /// and a member who blocked must not be written to. One call for the whole conversation rather
    /// than one per counterpart — a group of eight would otherwise be eight round trips per send.
    /// </para>
    /// </summary>
    public async Task<bool> IsBlockedWithinAsync(
        Conversation conversation, UserId viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentNullException.ThrowIfNull(viewer);

        var others = conversation.ActiveParticipants
            .Where(participant => participant.UserId != viewer)
            .Select(participant => participant.UserId)
            .ToList();

        if (others.Count == 0)
        {
            return false;
        }

        var blocked = await _blocks.FilterBlockedAsync(viewer, others, cancellationToken);

        return blocked.Count > 0;
    }

    /// <summary>Whether these two may open a conversation at all.</summary>
    public Task<bool> AreBlockedAsync(
        UserId first, UserId second, CancellationToken cancellationToken) =>
        _blocks.AreBlockedAsync(first, second, cancellationToken);
}
