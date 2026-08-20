using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Discovery;

/// <summary>
/// Reads one guide in full, by identifier.
/// <para>
/// Anonymous callers are served, deliberately: published knowledge is the reason the platform
/// exists, and a newcomer who cannot read a single guide before signing up has no reason to.
/// Visibility is still applied — a draft, a hidden guide and a deleted one all resolve as not
/// found to anybody but their author and staff.
/// </para>
/// </summary>
public sealed record GetGuideQuery : IQuery<GuideResponse>
{
    public required Guid GuideId { get; init; }
}
