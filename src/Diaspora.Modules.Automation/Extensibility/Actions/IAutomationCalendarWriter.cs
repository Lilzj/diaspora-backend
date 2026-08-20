using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Automation.Extensibility.Actions;

/// <summary>What an unattended calendar write produced.</summary>
/// <param name="Succeeded">Whether the entry was written, updated or removed.</param>
/// <param name="ExternalId">The provider's identifier, so a later update is an update.</param>
/// <param name="NotConfigured">
/// True when this deployment has no calendar provider. Distinct from a failure: the honest run record
/// says the platform cannot do this rather than that it tried and could not.
/// </param>
/// <param name="Message">Why, in words a member can read. Never a provider payload.</param>
public readonly record struct AutomationCalendarResult(
    bool Succeeded,
    string? ExternalId,
    bool NotConfigured,
    string? Message)
{
    public static AutomationCalendarResult Unconfigured(string message) =>
        new(false, null, true, message);
}

/// <summary>
/// The seam by which an automation writes into a member's connected calendar without the app open.
/// <para>
/// <b>FRONTEND-PHASE18-GAP-025, and it is an external dependency by construction.</b> The register
/// puts the capability with the integration platform and this module does not take it back: there is
/// no <c>HttpClient</c> in this assembly, no provider name, no OAuth token and no calendar model. What
/// this interface does is give the action somewhere to go, so that the day a deployment configures a
/// calendar adapter the automation half is already written and tested.
/// </para>
/// <para>
/// <b>The shipped implementation answers "not configured" and that is the correct behaviour, not a
/// placeholder.</b> B22 registers a <c>NotConfiguredCalendarProvider</c> for exactly the same reason
/// and records the same rationale: a missing registration is a resolution failure at the first
/// request, and a registered default that answers honestly is something a run record and a capability
/// report can both read. Nothing here claims a capability that does not exist.
/// </para>
/// </summary>
public interface IAutomationCalendarWriter
{
    /// <summary>Whether this deployment can write a calendar entry unattended at all.</summary>
    bool IsConfigured { get; }

    Task<AutomationCalendarResult> AddAsync(
        UserId member,
        string subjectKind,
        Guid subjectId,
        string title,
        DateTimeOffset startsAt,
        DateTimeOffset? endsAt,
        string timeZoneId,
        CancellationToken cancellationToken = default);

    Task<AutomationCalendarResult> UpdateAsync(
        UserId member,
        string subjectKind,
        Guid subjectId,
        string title,
        DateTimeOffset startsAt,
        DateTimeOffset? endsAt,
        string timeZoneId,
        CancellationToken cancellationToken = default);

    Task<AutomationCalendarResult> RemoveAsync(
        UserId member,
        string subjectKind,
        Guid subjectId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The honest default: this deployment has no calendar provider configured.
/// </summary>
public sealed class NotConfiguredCalendarWriter : IAutomationCalendarWriter
{
    private const string Message =
        "This deployment cannot write to a connected calendar on your behalf. Add it from the event "
        + "screen while the app is open instead.";

    public bool IsConfigured => false;

    public Task<AutomationCalendarResult> AddAsync(
        UserId member,
        string subjectKind,
        Guid subjectId,
        string title,
        DateTimeOffset startsAt,
        DateTimeOffset? endsAt,
        string timeZoneId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(AutomationCalendarResult.Unconfigured(Message));

    public Task<AutomationCalendarResult> UpdateAsync(
        UserId member,
        string subjectKind,
        Guid subjectId,
        string title,
        DateTimeOffset startsAt,
        DateTimeOffset? endsAt,
        string timeZoneId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(AutomationCalendarResult.Unconfigured(Message));

    public Task<AutomationCalendarResult> RemoveAsync(
        UserId member,
        string subjectKind,
        Guid subjectId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(AutomationCalendarResult.Unconfigured(Message));
}
