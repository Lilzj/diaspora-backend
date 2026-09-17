using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Responses;

namespace Diaspora.Modules.Community.Application.Capabilities;

/// <summary>What the caller may publish on the alert map.</summary>
public sealed record GetAlertCapabilitiesQuery : IQuery<AlertCapabilitiesResponse>;
