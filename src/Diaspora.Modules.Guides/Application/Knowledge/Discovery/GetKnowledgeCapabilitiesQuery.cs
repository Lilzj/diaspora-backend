using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Knowledge.Discovery;

/// <summary>
/// What this deployment's Knowledge Directory can actually do.
/// <para>
/// Every seam in this phase is optional, replaceable or additive, which means no client can assume
/// what is installed: a deployment may register three extra section providers, replace the search
/// engine, turn caching off and receive context bands from four verticals. A client that hard-coded
/// the shipped list would render a page missing half of it.
/// </para>
/// </summary>
public sealed record GetKnowledgeCapabilitiesQuery : IQuery<KnowledgeCapabilitiesResponse>;
