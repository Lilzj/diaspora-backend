using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Guides.Application.Engagement;

/// <summary>
/// Records that a reader shared the guide.
/// <para>
/// The platform cannot observe a share — it happens in a messaging app it has no window into —
/// so the client reports it. Unauthorized for the same reason views are: the people forwarding
/// a visa guide to a relative are frequently not signed in, and a share counter that only
/// counted members would understate the one number that measures whether the knowledge base is
/// doing its job.
/// </para>
/// </summary>
[Auditable]
public sealed record ShareGuideCommand : ICommand
{
    public required Guid GuideId { get; init; }
}
