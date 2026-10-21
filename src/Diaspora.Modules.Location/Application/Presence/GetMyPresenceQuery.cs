using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;

namespace Diaspora.Modules.Location.Application.Presence;

/// <summary>The caller's "when the app is open" setting — 21 Oct 2026.</summary>
[Authorize(Permission = LocationPermissions.ReadProfile)]
public sealed record GetMyPresenceQuery : IQuery<PresenceResponse>;
