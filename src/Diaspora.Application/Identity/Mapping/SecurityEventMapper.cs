using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Security;

namespace Diaspora.Application.Identity.Mapping;

/// <summary>
/// Projects the security log for the member's own review. Metadata is withheld — it is
/// operational detail, and some of it would help an attacker who already has the session.
/// </summary>
public static class SecurityEventMapper
{
    public static SecurityEventResponse ToResponse(SecurityEvent securityEvent)
    {
        ArgumentNullException.ThrowIfNull(securityEvent);

        return new SecurityEventResponse
        {
            Id = securityEvent.Id.Value,
            Type = securityEvent.Type.ToString(),
            Severity = securityEvent.Severity.ToString(),
            Description = securityEvent.Description,
            IpAddress = securityEvent.IpAddress,
            OccurredAt = securityEvent.OccurredAt
        };
    }

    public static IReadOnlyList<SecurityEventResponse> ToResponses(IEnumerable<SecurityEvent> events) =>
        events.Select(ToResponse).ToArray();
}
