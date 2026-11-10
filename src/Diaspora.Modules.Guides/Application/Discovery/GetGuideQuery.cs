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
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record GetGuideQuery : IQuery<GuideResponse>
{
    public required Guid GuideId { get; init; }
}
