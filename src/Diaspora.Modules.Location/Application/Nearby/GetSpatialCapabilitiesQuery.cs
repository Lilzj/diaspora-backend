using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;

namespace Diaspora.Modules.Location.Application.Nearby;

/// <summary>
/// What this deployment's geography can actually do.
/// <para>
/// Served so a client can hide a control its backend cannot support, and so an operator can
/// confirm that the provider they configured is the one running — a question that otherwise
/// has no answer short of reading startup logs.
/// </para>
/// </summary>
[RequirePermission(LocationPermissions.ReadCatalog)]
public sealed record GetSpatialCapabilitiesQuery : IQuery<SpatialCapabilitiesResponse>;
