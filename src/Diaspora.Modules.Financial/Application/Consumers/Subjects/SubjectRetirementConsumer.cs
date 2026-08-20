using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Financial.Application.Consumers.Subjects;

/// <summary>
/// Ends paid placements over a subject that is no longer there.
/// <para>
/// <b>The base every "the thing you paid to promote has gone" consumer derives from.</b> A listing
/// is sold, a job is closed, a property is let, an event is cancelled, a provider is archived — in
/// every case the placement over it is still running and still being counted, and nobody is served
/// by a featured rail advertising something that cannot be bought.
/// </para>
/// <para>
/// <b>The refund question is deliberately not asked here.</b> A seller whose item sold on day two of
/// a seven-day boost got what they paid for; a refund is a decision a person makes against the
/// payment. Ending the placement and refunding it are different operations and this is only the
/// first.
/// </para>
/// <para>
/// <b>Idempotent by construction.</b> The aggregate ignores an end on an already-ended placement, so
/// a redelivered event does nothing at all — which is what at-least-once delivery requires.
/// </para>
/// </summary>
public abstract class SubjectRetirementConsumer<TIntegrationEvent>
    : IntegrationEventHandler<TIntegrationEvent>
    where TIntegrationEvent : IIntegrationEvent
{
    protected SubjectRetirementConsumer(
        IPlacementRepository placements,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger logger)
    {
        Placements = placements;
        UnitOfWork = unitOfWork;
        Clock = clock;
        Logger = logger;
    }

    protected IPlacementRepository Placements { get; }

    protected IUnitOfWork UnitOfWork { get; }

    protected IClock Clock { get; }

    protected ILogger Logger { get; }

    /// <summary>What kind of thing this consumer's contract is about.</summary>
    protected abstract MonetizationSubjectType SubjectType { get; }

    /// <summary>
    /// The subject the event retires, or null when it retires nothing.
    /// <para>
    /// Null is normal and common: a provider whose status changed to "on holiday" has not gone
    /// anywhere, and a partially-let property is still worth promoting.
    /// </para>
    /// </summary>
    protected abstract Guid? RetiredSubjectId(TIntegrationEvent integrationEvent);

    /// <summary>Why, for the placement's own record.</summary>
    protected abstract string Reason { get; }

    protected override async Task HandleAsync(
        TIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (RetiredSubjectId(integrationEvent) is not { } subjectId || subjectId == Guid.Empty)
        {
            return;
        }

        var now = Clock.UtcNow;

        var placements = await Placements.ListActiveForSubjectAsync(
            SubjectReference.Create(SubjectType, subjectId), now, cancellationToken);

        if (placements.Count == 0)
        {
            return;
        }

        foreach (var placement in placements)
        {
            placement.Cancel(Reason, now);
            Placements.Update(placement);
        }

        await UnitOfWork.SaveChangesAsync(cancellationToken);

        Logger.LogInformation(
            "Ended {Count} placement(s) over {SubjectType} {SubjectId}: {Reason}",
            placements.Count,
            SubjectType,
            subjectId,
            Reason);
    }
}
