using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Contracts.V1.Responses;

namespace Diaspora.Modules.Media.Application.Collections;

/// <summary>Reads a collection and every asset in it, in render order.</summary>
[RequirePolicy(MediaPolicies.Read)]
public sealed record GetCollectionQuery : IQuery<MediaCollectionResponse>
{
    public required Guid CollectionId { get; init; }
}
