using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Conversations.Application.Mapping;
using Diaspora.Modules.Conversations.Application.Services;
using Diaspora.Modules.Conversations.Contracts.V1.Requests;
using Diaspora.Modules.Conversations.Contracts.V1.Responses;
using Diaspora.Modules.Conversations.Domain.Conversations;
using Diaspora.Modules.Conversations.Domain.Enums;
using Diaspora.Modules.Conversations.Domain.Errors;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Modules.Conversations.Domain.Messages;
using Diaspora.Modules.Conversations.Domain.ValueObjects;
using Diaspora.Modules.Conversations.Extensibility;
using Diaspora.Modules.Conversations.Observability;
using Diaspora.Modules.Conversations.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Conversations.Application.Conversations;

/// <summary>
/// Find-or-create, which is what the client's single entry point needs.
/// <para>
/// <c>ConversationHub.openOrCreateConversation</c> is the one door every module in the app goes
/// through, precisely so a buyer who taps "Message seller" twice does not end up with two threads.
/// The server enforces the same rule — a client-side check is a suggestion, and two devices tapping
/// at once defeats it entirely.
/// </para>
/// </summary>
[Authorize(Permission = ConversationPermissions.Participate)]
[Transactional(Reason =
    "Opens the conversation, both participant rows and the opening message as one unit. A "
    + "conversation that committed without its first message would be an empty thread the client "
    + "renders and nobody can explain.")]
public sealed record CreateConversationCommand : ICommand<ConversationResponse>
{
    public required Guid RecipientId { get; init; }

    public required ConversationContextRequest Context { get; init; }

    public string? InitialMessage { get; init; }

    /// <summary>
    /// Set by the module's own integration adapters, never by an endpoint.
    /// <para>
    /// A system-managed context type — a support ticket, a trust appeal — must not be openable by a
    /// client, or anybody could manufacture a thread that looks official. The endpoint leaves this
    /// false; the Marketplace adapter and the event consumers set it.
    /// </para>
    /// </summary>
    public bool AllowSystemManaged { get; init; }
}

public sealed class CreateConversationCommandValidator : AbstractValidator<CreateConversationCommand>
{
    public CreateConversationCommandValidator()
    {
        RuleFor(command => command.RecipientId).NotEmpty();
        RuleFor(command => command.Context).NotNull();
        RuleFor(command => command.Context.Type).NotEmpty().MaximumLength(ConversationType.MaxLength);

        RuleFor(command => command.Context.EntityId)
            .NotEmpty()
            .MaximumLength(ConversationContext.MaxEntityIdLength);

        RuleFor(command => command.InitialMessage)
            .MaximumLength(Domain.Policies.ConversationLimits.MaxMessageLength);
    }
}

public sealed class CreateConversationCommandHandler
    : ICommandHandler<CreateConversationCommand, ConversationResponse>
{
    private readonly IConversationRepository _conversations;
    private readonly IConversationMessageRepository _messages;
    private readonly ConversationAccessService _access;
    private readonly ConversationContextCatalog _catalog;
    private readonly ConversationMapper _mapper;
    private readonly IEnumerable<IConversationPolicy> _policies;
    private readonly IOptionsMonitor<ConversationOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public CreateConversationCommandHandler(
        IConversationRepository conversations,
        IConversationMessageRepository messages,
        ConversationAccessService access,
        ConversationContextCatalog catalog,
        ConversationMapper mapper,
        IEnumerable<IConversationPolicy> policies,
        IOptionsMonitor<ConversationOptions> options,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _conversations = conversations;
        _messages = messages;
        _access = access;
        _catalog = catalog;
        _mapper = mapper;
        _policies = policies;
        _options = options;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ConversationResponse>> Handle(
        CreateConversationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<ConversationResponse>(caller.Error);
        }

        var initiator = caller.Value;
        var recipient = UserId.From(command.RecipientId);

        if (initiator == recipient)
        {
            return Result.Failure<ConversationResponse>(ConversationErrors.SelfConversation);
        }

        var contextType = await _catalog.FindAsync(command.Context.Type, cancellationToken);

        if (contextType is null)
        {
            return Result.Failure<ConversationResponse>(ConversationErrors.UnknownContextType);
        }

        if (contextType.IsSystemManaged && !command.AllowSystemManaged)
        {
            // Deliberately the same error as an unknown type. Telling a caller "this type exists but
            // you may not use it" is telling them which types are worth probing for.
            return Result.Failure<ConversationResponse>(ConversationErrors.UnknownContextType);
        }

        var type = ConversationType.From(contextType.Key);
        var reference = ConversationReference.Create(type, command.Context.EntityId);
        var now = _clock.UtcNow;

        // Find first. The client checks its own store before calling, so reaching here usually means
        // it has no local copy — which happens on a fresh install, on a second device, and after any
        // cache clear. Returning the existing conversation is what stops those becoming duplicates.
        var existing = await _conversations.FindByReferenceAsync(
            reference, initiator, recipient, cancellationToken);

        if (existing is not null)
        {
            ConversationTelemetry.ConversationsReused.Add(
                1, new KeyValuePair<string, object?>("context_type", type.Key));

            // Nothing was staged, so nothing is saved. Find-or-create that found is a read.
            return await RenderAsync(existing, initiator, cancellationToken);
        }

        if (await _access.AreBlockedAsync(initiator, recipient, cancellationToken))
        {
            return Result.Failure<ConversationResponse>(ConversationErrors.Blocked);
        }

        // Additive, refusal-only policies. Trust's account-level bans and a deployment's own rules
        // arrive here; none of them can permit something already refused above.
        foreach (var policy in _policies)
        {
            var decision = await policy.EvaluateAsync(
                new ConversationPolicyContext
                {
                    InitiatorUserId = initiator,
                    RecipientUserId = recipient,
                    ContextTypeKey = type.Key,
                    EntityId = reference.EntityId
                },
                cancellationToken);

            if (!decision.Allowed)
            {
                return Result.Failure<ConversationResponse>(Error.Forbidden(
                    ConversationErrorCodes.Blocked,
                    decision.Reason ?? "You cannot start this conversation."));
            }
        }

        var context = ConversationContext.Create(
            type,
            reference.EntityId,
            command.Context.EntityTitle ?? string.Empty,
            now,
            command.Context.EntitySubtitle,
            command.Context.EntityImageMediaId,
            command.Context.EntityPrice,
            command.Context.EntityStatus,
            command.Context.EntityStatusLabel,
            command.Context.DeepLink);

        var opened = Conversation.Open(
            ConversationId.From(_guids.NewSequentialGuid()),
            context,
            initiator,
            recipient,
            now,
            ConversationMetadata.Create(
                command.Context.CampaignSource,
                command.Context.ReferralCode,
                command.Context.AppVersion),

            // The catalogue decides per type; the option is the floor. A deployment with requests
            // switched off has no gate on any type.
            requiresAcceptance:
                contextType.RequiresAcceptance && _options.CurrentValue.EnableMessageRequests);

        if (opened.IsFailure)
        {
            return Result.Failure<ConversationResponse>(opened.Error);
        }

        var conversation = opened.Value;

        _conversations.Add(conversation);

        if (!string.IsNullOrWhiteSpace(command.InitialMessage))
        {
            var message = ConversationMessage.Send(
                ConversationMessageId.From(_guids.NewSequentialGuid()),
                conversation.Id,
                initiator,
                command.InitialMessage,
                now);

            if (message.IsSuccess)
            {
                var recorded = conversation.RecordMessage(message.Value.Id, initiator, now);

                if (recorded.IsSuccess)
                {
                    message.Value.DeclareRecipients(recorded.Value);
                    _messages.Add(message.Value);
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        ConversationTelemetry.ConversationsOpened.Add(
            1, new KeyValuePair<string, object?>("context_type", type.Key));

        return await RenderAsync(conversation, initiator, cancellationToken);
    }

    private async Task<Result<ConversationResponse>> RenderAsync(
        Conversation conversation, UserId viewer, CancellationToken cancellationToken)
    {
        var context = await _mapper.BuildContextAsync(viewer, [conversation], cancellationToken);

        var last = conversation.LastMessageId is null
            ? null
            : await _messages.GetAsync(conversation.LastMessageId, cancellationToken);

        return Result.Success(ConversationMapper.ToResponse(conversation, context, last));
    }
}
