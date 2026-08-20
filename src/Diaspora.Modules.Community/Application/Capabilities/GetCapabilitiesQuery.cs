using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Responses;

namespace Diaspora.Modules.Community.Application.Capabilities;

/// <summary>What this deployment of Community can do. Read by a client at startup.</summary>
public sealed record GetCapabilitiesQuery : IQuery<CommunityCapabilitiesResponse>;
