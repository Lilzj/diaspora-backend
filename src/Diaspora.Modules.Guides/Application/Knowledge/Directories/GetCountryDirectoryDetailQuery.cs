using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Knowledge.Directories;

/// <summary>
/// One country directory as a curator sees it: hidden rows, unpublished state, translations.
/// <para>
/// A separate query from the reader's country page rather than the same one with more fields. The
/// reader's response is filtered, and one contract that sometimes carried both would make every
/// consumer's rendering depend on who was asking.
/// </para>
/// </summary>
[Authorize(Permission = KnowledgePermissions.ManageDirectory)]
public sealed record GetCountryDirectoryDetailQuery : IQuery<CountryDirectoryDetailResponse>
{
    public required string CountryCode { get; init; }

    /// <summary>Include an archived directory. What the archive screen asks for.</summary>
    public bool IncludeArchived { get; init; }
}
