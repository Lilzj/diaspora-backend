using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Contracts.V1.Requests;
using Diaspora.Modules.Media.Contracts.V1.Responses;

namespace Diaspora.Modules.Media.Application.Collections;

/// <summary>Creates a gallery, an attachment set, or any other ordered group of assets.</summary>
[Auditable]
public sealed record CreateCollectionCommand : ICommand<MediaCollectionResponse>
{
    public required CreateCollectionRequest Request { get; init; }
}
