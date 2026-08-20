using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Events;

/// <summary>
/// Raised when community corroboration moves a post across a threshold.
/// <para>
/// This is the event a notification module wants and a ranking cache must not ignore: a post
/// becoming community-verified is the single largest ranking change that can happen to it
/// without anybody editing anything.
/// </para>
/// </summary>
public sealed record PostVerificationChangedDomainEvent : DomainEvent
{
    public required CommunityPostId PostId { get; init; }

    public required PostVerificationStatus Previous { get; init; }

    public required PostVerificationStatus Current { get; init; }

    public required int Confirmations { get; init; }

    public required int VerifiedConfirmations { get; init; }
}
