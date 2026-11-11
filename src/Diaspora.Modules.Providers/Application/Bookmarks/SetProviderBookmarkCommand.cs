using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Providers.Application.Bookmarks;

/// <summary>
/// Saves or unsaves a provider.
/// <para>
/// One command for both directions, because the client's control is a toggle and a toggle that
/// maps to two commands has to decide which one to send — from state it may not have, on a
/// screen that may have been open for an hour. An idempotent set is the honest shape.
/// </para>
/// <para>
/// Not <c>[Transactional]</c>: the write lands in Community's aggregate, not B7's, and wrapping
/// a call into another module's handler in this module's transaction is how two modules end up
/// sharing a unit of work they cannot both reason about.
/// </para>
/// </summary>
[Auditable]
[RequirePolicy(ProviderPolicies.Bookmark)]
public sealed record SetProviderBookmarkCommand : ICommand
{
    public required Guid ProviderId { get; init; }

    public required bool IsSaved { get; init; }
}
