using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Guides.Application.Knowledge.Directories;

/// <summary>
/// Removes a country from the platform's knowledge surface entirely.
/// <para>
/// Distinct from unpublishing: an unpublished directory is still on the curator's desk, and an
/// archived one is not. The arrangement is kept — archiving is a soft delete — so a country the
/// platform stops serving can be brought back without being rebuilt.
/// </para>
/// </summary>
[Authorize(Permission = KnowledgePermissions.ManageDirectory)]
[Auditable]
public sealed record ArchiveCountryDirectoryCommand : ICommand
{
    public required string CountryCode { get; init; }
}
