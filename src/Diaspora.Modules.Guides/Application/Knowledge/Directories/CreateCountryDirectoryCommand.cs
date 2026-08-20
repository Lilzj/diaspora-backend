using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Knowledge.Directories;

/// <summary>
/// Opens a knowledge directory for a country.
/// <para>
/// Created unpublished, and arranged from whichever template applies. Publishing is a separate,
/// deliberate act — a country page is arranged over weeks and must not be half-visible while it is.
/// </para>
/// <para>
/// Gated at <see cref="KnowledgePermissions.ManageDirectory"/>, role-scoped to administrators:
/// which countries the platform serves is a structural decision, not an editorial one.
/// </para>
/// </summary>
[Authorize(Permission = KnowledgePermissions.ManageDirectory)]
[Auditable]
public sealed record CreateCountryDirectoryCommand : ICommand<CountryDirectoryDetailResponse>
{
    /// <summary>ISO 3166-1 alpha-2, from the route. The resource's identity.</summary>
    public required string CountryCode { get; init; }

    public required CreateCountryDirectoryRequest Request { get; init; }
}
