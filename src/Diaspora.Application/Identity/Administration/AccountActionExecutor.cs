using System.Globalization;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Security;
using Diaspora.Domain.Identity.Sessions;
using Diaspora.Domain.Identity.Users;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Administration;

/// <summary>
/// Who took an action on an account: the member of staff who carried it out, and, when a second
/// member of staff confirmed somebody else's proposal, the one who proposed it.
/// </summary>
public sealed record StaffAttribution(UserId ActedBy, UserId? ProposedBy = null);

/// <summary>
/// Carries out a staff action on an account that has already passed every check.
/// <para>
/// One place for what an action does — the account's own method, the sessions it ends, the line
/// in the member's security history naming both members of staff — so an action taken at once and
/// the same action confirmed from a proposal cannot drift apart. Nothing here saves: the caller's
/// unit of work does, with the proposal in the same save.
/// </para>
/// </summary>
public sealed class AccountActionExecutor
{
    private readonly IUserRepository _users;
    private readonly ISessionRevocationService _revocation;
    private readonly ISecurityEventWriter _securityEvents;

    public AccountActionExecutor(
        IUserRepository users,
        ISessionRevocationService revocation,
        ISecurityEventWriter securityEvents)
    {
        _users = users;
        _revocation = revocation;
        _securityEvents = securityEvents;
    }

    /// <summary>
    /// Suspends the account and cuts its live sessions: a suspension that only took effect at the
    /// next token expiry would not be one. The member can still sign in, to the suspension screen.
    /// </summary>
    public async Task<Result> SuspendAsync(
        User target,
        string reasonCode,
        string note,
        DateTimeOffset? until,
        StaffAttribution attribution,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(attribution);

        var suspension = target.Suspend(note, until, now, reasonCode);
        if (suspension.IsFailure)
        {
            return suspension;
        }

        _users.Update(target);

        var revoked = await _revocation.RevokeAllAsync(
            target.Id, SessionRevocationReason.AccountStateChanged, now, cancellationToken);

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = target.Id,
            Type = SecurityEventType.AccountSuspended,
            Severity = SecurityEventSeverity.Warning,
            Description = $"Account suspended; {revoked} session(s) revoked.",
            Metadata = Describe(attribution, reasonCode, note, new Dictionary<string, string>
            {
                ["until"] = until?.ToString("O", CultureInfo.InvariantCulture) ?? "indefinite"
            })
        }, cancellationToken);

        return Result.Success();
    }

    /// <summary>Closes the account. Sessions end and every issued token stops working.</summary>
    public async Task<Result> DisableAsync(
        User target,
        string reasonCode,
        string note,
        StaffAttribution attribution,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(attribution);

        var disable = target.Disable(note, now, reasonCode);
        if (disable.IsFailure)
        {
            return disable;
        }

        _users.Update(target);

        var revoked = await _revocation.RevokeAllAsync(
            target.Id, SessionRevocationReason.AccountStateChanged, now, cancellationToken);

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = target.Id,
            Type = SecurityEventType.AccountDisabled,
            Severity = SecurityEventSeverity.Warning,
            Description = $"Account disabled; {revoked} session(s) revoked.",
            Metadata = Describe(attribution, reasonCode, note)
        }, cancellationToken);

        return Result.Success();
    }

    /// <summary>Reopens a closed account at limited access: how a closed member's appeal is honoured.</summary>
    public async Task<Result> ReactivateAsync(
        User target,
        string? reasonCode,
        string note,
        StaffAttribution attribution,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(attribution);

        var reopened = target.Reactivate(now, note);
        if (reopened.IsFailure)
        {
            return reopened;
        }

        _users.Update(target);

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = target.Id,
            Type = SecurityEventType.AccountReinstated,
            Severity = SecurityEventSeverity.Notice,
            Description = $"Closed account reopened at {target.AccessState.Name}.",
            Metadata = Describe(attribution, reasonCode, note)
        }, cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// Gives the account a role, and signs it out everywhere.
    /// <para>
    /// <b>The sign-out is the point.</b> Permissions travel inside the access token, so somebody
    /// demoted keeps every permission they held until the token expires, and somebody promoted gains
    /// nothing until theirs does. Revoking forces a fresh token carrying the role they now have.
    /// </para>
    /// </summary>
    public async Task<int> ChangeRoleAsync(
        User target,
        UserRole role,
        string reason,
        StaffAttribution attribution,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(attribution);

        var previous = target.Role;

        target.AssignRole(role, reason, now);
        _users.Update(target);

        var revoked = await _revocation.RevokeAllAsync(
            target.Id, SessionRevocationReason.AccountStateChanged, now, cancellationToken);

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = target.Id,
            Type = SecurityEventType.RoleAssigned,
            Severity = SecurityEventSeverity.Warning,
            Description = $"Role changed from {previous.Name} to {role.Name}: {reason}",
            Metadata = Describe(attribution, reasonCode: null, reason, new Dictionary<string, string>
            {
                ["previousRole"] = previous.Name,
                ["newRole"] = role.Name,
                ["sessionsRevoked"] = revoked.ToString(CultureInfo.InvariantCulture),
                // The name the security history used before 7 Nov 2026, kept for whoever reads it.
                ["changedBy"] = attribution.ActedBy.Value.ToString()
            })
        }, cancellationToken);

        return revoked;
    }

    private static Dictionary<string, string> Describe(
        StaffAttribution attribution,
        string? reasonCode,
        string note,
        Dictionary<string, string>? extra = null)
    {
        var metadata = extra ?? new Dictionary<string, string>();

        metadata["reason"] = note;

        if (!string.IsNullOrWhiteSpace(reasonCode))
        {
            metadata["reasonCode"] = reasonCode;
        }

        if (attribution.ProposedBy is { } proposer)
        {
            metadata["proposedBy"] = proposer.Value.ToString();
            metadata["confirmedBy"] = attribution.ActedBy.Value.ToString();
        }
        else
        {
            metadata["actedBy"] = attribution.ActedBy.Value.ToString();
        }

        return metadata;
    }
}
