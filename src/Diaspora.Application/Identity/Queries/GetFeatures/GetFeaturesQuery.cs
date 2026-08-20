using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Queries.GetFeatures;

/// <summary>
/// Returns every feature gate and whether it is open, so a client can grey out what is
/// unavailable rather than discovering it through a 403. Backs <c>GET /access/features</c>.
/// </summary>
[Authorize(Permission = "identity:profile:read")]
public sealed record GetFeaturesQuery : IQuery<FeatureAccessResponse>;
