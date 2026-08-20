using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Contracts.V1.Requests;
using Diaspora.Modules.Media.Contracts.V1.Responses;

namespace Diaspora.Modules.Media.Application.Assets;

/// <summary>
/// Claims an uploaded asset for an owning entity.
/// <para>
/// The call every vertical makes after saving its own record: "this photograph belongs to
/// housing listing 7f3c…, as its cover". Media stores the pairing and learns nothing else
/// about housing listings.
/// </para>
/// </summary>
[Auditable]
public sealed record AttachMediaCommand : ICommand<MediaAssetResponse>
{
    public required AttachMediaRequest Request { get; init; }
}
