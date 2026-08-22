using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Members;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Application.EventHandlers;

/// <summary>
/// Keeps Community's member projection.
/// <para>
/// <b>The first integration-event consumer in this module, and BG-05a is why.</b> Community is a
/// reusable social foundation that deliberately knows nothing about the modules above it and reaches
/// into neither Identity's domain nor Trust's — so until now it had no way to answer whether a
/// member exists, and reporting a person was the one moderation subject the platform refused.
/// </para>
/// <para>
/// <c>UserRegisteredIntegrationEvent</c> is a published contract in <c>Diaspora.Contracts</c>, not
/// Identity's domain, and its own summary asks for exactly this use:
/// <i>"other modules use this to seed their own read models; they must not query Identity's tables
/// directly."</i>
/// </para>
/// <para>
/// <b>Idempotent, because delivery is at-least-once.</b> A redelivered registration finds the row
/// and writes nothing. <see cref="CommunityMember.Refresh"/> returns whether anything moved so that
/// the common case costs one read and no write.
/// </para>
/// <para>
/// <b>Members who registered before this shipped are backfilled by migration</b>
/// (<c>ProjectCommunityMembers</c>), not by this consumer — an outbox holds no history. Without that
/// backfill this projection would know only accounts created after deployment, and reporting an
/// established member would answer <c>community.content.not-found</c>: the same failure BG-05a
/// exists to remove, arriving from the opposite direction.
/// </para>
/// </summary>
public sealed class UserRegisteredCommunityConsumer
    : IntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    private readonly ICommunityMemberDirectory _members;
    private readonly IUnitOfWork _unitOfWork;

    public UserRegisteredCommunityConsumer(
        ICommunityMemberDirectory members, IUnitOfWork unitOfWork)
    {
        _members = members;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleAsync(
        UserRegisteredIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.UserId == Guid.Empty)
        {
            return;
        }

        var userId = UserId.From(integrationEvent.UserId);
        var existing = await _members.FindAsync(userId, cancellationToken);

        if (existing is null)
        {
            _members.Add(CommunityMember.Project(
                userId,
                integrationEvent.FullName,
                integrationEvent.CountryOfResidence,
                integrationEvent.OccurredAt));
        }
        else if (existing.Refresh(
            integrationEvent.FullName, integrationEvent.CountryOfResidence))
        {
            _members.Update(existing);
        }
        else
        {
            // A redelivery of an event that changed nothing. Saving here would write a row and
            // raise a concurrency token for no reason.
            return;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
