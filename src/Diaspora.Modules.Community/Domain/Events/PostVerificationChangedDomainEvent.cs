using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
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

    /// <summary>Whose post it is. Trust pays the author when the community verifies it.</summary>
    public required UserId AuthorId { get; init; }

    /// <summary>The post's country, for a reward that counts once per country. Null when it has none.</summary>
    public string? CountryCode { get; init; }
}
