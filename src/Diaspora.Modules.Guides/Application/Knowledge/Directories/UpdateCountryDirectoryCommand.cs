using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Knowledge.Directories;

/// <summary>Rewrites a country directory's header copy and its translations.</summary>
[RequirePermission(KnowledgePermissions.ManageDirectory)]
[Auditable]
public sealed record UpdateCountryDirectoryCommand : ICommand<CountryDirectoryDetailResponse>
{
    public required string CountryCode { get; init; }

    public required UpdateCountryDirectoryRequest Request { get; init; }
}
