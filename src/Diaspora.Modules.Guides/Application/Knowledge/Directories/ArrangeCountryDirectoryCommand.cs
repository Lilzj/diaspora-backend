using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Knowledge.Directories;

/// <summary>
/// Replaces a country directory's whole arrangement, in the order given.
/// <para>
/// <b>The command the brief calls "Manage Category Ordering", and it is whole-list replacement
/// rather than a set of move operations.</b> A drag-and-drop curation screen already holds the final
/// ordering; expressing it as a sequence of moves would mean the client computing a diff and the
/// server replaying it, with two chances to disagree about the result and no way for either to
/// notice.
/// </para>
/// <para>
/// Every key is checked against the catalogue before anything is stored. An entry pointing at a row
/// that does not exist would render as a raw key on a country page, which is a visible defect on the
/// most-read screen in the product.
/// </para>
/// </summary>
[RequirePermission(KnowledgePermissions.ManageDirectory)]
[Auditable]
public sealed record ArrangeCountryDirectoryCommand : ICommand<CountryDirectoryDetailResponse>
{
    public required string CountryCode { get; init; }

    public required ArrangeCountryDirectoryRequest Request { get; init; }
}
