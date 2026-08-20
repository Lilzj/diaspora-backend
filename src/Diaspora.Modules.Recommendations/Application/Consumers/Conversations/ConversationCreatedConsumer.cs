using Diaspora.Contracts.Conversations.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Conversations;

/// <summary>
/// Somebody opened a conversation about something.
/// <para>
/// <b>The strongest intent signal on the platform, and until PSR-1.2 nothing read it.</b> Every
/// other engagement this module observes is one-sided — a view, a save, a reaction. Opening a thread
/// is the member putting their name to a question and waiting for a stranger to answer it: it is
/// what a job application is to Jobs and what a viewing is to Housing, and both of those have been
/// weighted <see cref="SignalKind.Applied"/> since B14. The enum's own wording covers this case
/// exactly — <em>"applied for a job, enquired about a listing, or requested a viewing"</em> — and
/// enquiring about a listing was the one of the three nothing produced.
/// </para>
/// <para>
/// <b>Only the initiator signals interest.</b> A conversation has at least two participants and only
/// one of them chose to be in it. Crediting the seller with an affinity for their own listing would
/// teach the platform to recommend people the things they are already selling, and the error
/// compounds: the busier a seller is, the more wrongly they are profiled.
/// </para>
/// <para>
/// <b>Nothing about the conversation itself is recorded.</b> Not the thread identifier, not the
/// counterpart, not the title. The signal says "this member is interested in that listing", which is
/// the same sentence a view produces; who they are talking to and what they said stay in the one
/// table with the one access rule. That is also why the contract carries no body for this module to
/// have refused.
/// </para>
/// </summary>
public sealed class ConversationCreatedConsumer
    : RecommendationEventConsumer<ConversationCreatedIntegrationEvent>
{
    public ConversationCreatedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Conversations;

    protected override Task HandleAsync(
        ConversationCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var targetType = ConversationContextMap.Translate(integrationEvent.ContextType);

        if (targetType is null)
        {
            return Task.CompletedTask;
        }

        // The entity identifier is opaque on the wire — the owning module decides what it is, and
        // three of the twelve context types could reasonably carry something that is not a Guid. A
        // signal about a target this module cannot address is a row that joins to nothing.
        if (!Guid.TryParse(integrationEvent.EntityId, out var targetId) || targetId == Guid.Empty)
        {
            return Task.CompletedTask;
        }

        return RecordAsync(
            Signal(
                UserId.From(integrationEvent.InitiatorUserId),
                SignalKind.Applied,
                integrationEvent.CreatedAt,
                targetType,
                targetId),
            cancellationToken);
    }
}
