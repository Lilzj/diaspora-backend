using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Identity;

/// <summary>
/// A member's account state moved.
/// <para>
/// <b>Consumed for one thing: whether onboarding is finished.</b> That single fact decides whether
/// the onboarding policy demotes the transactional target types, which is the difference between a
/// half-registered member seeing a page of jobs they cannot apply for and a page of guides
/// explaining how to become able to.
/// </para>
/// <para>
/// <b>The state names are Identity's, and this module deliberately does not model them.</b> B2.0.1
/// established that the nine-state machine is Identity's and the coarse tier is what everybody else
/// reasons about; matching on a single well-known name here is the narrowest possible dependency on
/// somebody else's vocabulary, and it degrades to "not finished" rather than throwing if Identity
/// renames a state.
/// </para>
/// </summary>
public sealed class UserAccessStateChangedConsumer
    : RecommendationEventConsumer<UserAccessStateChangedIntegrationEvent>
{
    /// <summary>Identity's name for a finished account. See the class summary.</summary>
    private const string OnboardedState = "Active";

    public UserAccessStateChangedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Identity;

    protected override Task HandleAsync(
        UserAccessStateChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var complete = string.Equals(
            integrationEvent.ToState, OnboardedState, StringComparison.OrdinalIgnoreCase);

        return RecordAsync(
            Signal(
                UserId.From(integrationEvent.UserId),
                SignalKind.Onboarding,
                integrationEvent.OccurredAt,
                key: complete ? "complete" : "incomplete"),
            cancellationToken);
    }
}
