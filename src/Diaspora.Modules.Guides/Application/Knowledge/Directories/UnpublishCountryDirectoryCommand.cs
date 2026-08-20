using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Guides.Application.Knowledge.Directories;

/// <summary>
/// Takes a country's directory off the reader-facing surface without losing the arrangement.
/// <para>
/// The state a curator needs when a country's rules change out from under a page: the arrangement
/// stays on their desk, the readers stop seeing it, and republishing is one call rather than a
/// rebuild.
/// </para>
/// </summary>
[Authorize(Permission = KnowledgePermissions.ManageDirectory)]
[Auditable]
public sealed record UnpublishCountryDirectoryCommand : ICommand
{
    public required string CountryCode { get; init; }
}
