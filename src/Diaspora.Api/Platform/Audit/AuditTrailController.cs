using Asp.Versioning;
using Diaspora.Api.Common;
using Diaspora.Contracts.Authorization;
using Diaspora.Domain.Identity.Users;
using Diaspora.Platform.Auditing;
using Diaspora.Platform.Http;
using Diaspora.Platform.Modules.Contributors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diaspora.Api.Platform.Audit;

public static class AuditPermissions
{
    public const string Read = "platform:audit:read";
}

public static class AuditPolicies
{
    public const string Read = "platform.audit.read";
}

/// <summary>
/// Who may read the audit trail: administrators, not moderators.
/// <para>
/// The trail is how staff are held to account (7 Nov 2026), and most of what it holds is what
/// moderators did. A moderator reading the record of their own decisions is not the check it is
/// there to be.
/// </para>
/// </summary>
public sealed class AuditAuthorizationContributor : IPermissionContributor, IPolicyContributor
{
    private static readonly IReadOnlySet<string> AdminAndAbove =
        new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(UserRole.Admin),
            nameof(UserRole.SuperAdmin)
        };

    public string Module => "Platform.Audit";

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        yield return new PermissionDefinition(
            AuditPermissions.Read,
            Module,
            "Read the audit trail: what staff and members did, to what, and the reason given.")
        {
            MinimumTier = null,
            Roles = AdminAndAbove
        };
    }

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        yield return PolicyDefinition.ForPermission(AuditPolicies.Read, Module, AuditPermissions.Read);
    }
}

/// <summary>One entry of the trail, as an administrator reads it.</summary>
public sealed record AuditRecordResponse(
    Guid Id,
    DateTimeOffset OccurredAt,
    string? ActorUserId,
    string? ActorRole,
    string Action,
    string EntityType,
    string? EntityId,
    string? Reason,
    string? Detail,
    string? CorrelationId);

/// <summary>
/// The stored audit trail, read back.
/// <para>
/// There was nothing to read until 7 Nov 2026: the trail was a log line. Filter by the thing acted
/// on (<c>entityId</c>), by who acted (<c>actorUserId</c>), by action or its prefix, and by window.
/// </para>
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiEndpoints.BaseUrl + "/platform/audit")]
[Produces("application/json")]
[Authorize(Policy = AuditPolicies.Read)]
public sealed class AuditTrailController : ApiController
{
    private readonly IAuditTrailReader _trail;

    public AuditTrailController(IAuditTrailReader trail)
    {
        _trail = trail;
    }

    [HttpGet("")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AuditRecordResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ReadAsync(
        [FromQuery] string? entityId,
        [FromQuery] string? actorUserId,
        [FromQuery] string? action,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var records = await _trail.ReadAsync(
            new AuditTrailQuery
            {
                EntityId = entityId,
                ActorUserId = actorUserId,
                Action = action,
                From = from,
                To = to,
                Limit = limit
            },
            cancellationToken);

        IReadOnlyList<AuditRecordResponse> body = [.. records.Select(record => new AuditRecordResponse(
            record.Id,
            record.OccurredAt,
            record.ActorUserId,
            record.ActorRole,
            record.Action,
            record.EntityType,
            record.EntityId,
            record.Reason,
            record.Detail,
            record.CorrelationId))];

        return Ok(new ApiResponse<IReadOnlyList<AuditRecordResponse>>(body, ApiMeta.For(HttpContext)));
    }
}
