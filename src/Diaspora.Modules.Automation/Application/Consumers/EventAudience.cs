namespace Diaspora.Modules.Automation.Application.Consumers;

/// <summary>
/// Whose automations may hear about an event — 21 Oct 2026.
/// <para>
/// <b>An automation must never see an event its owner could not have found.</b> The event triggers
/// pass an empty owner set — "anybody watching for this" — and until this existed they passed it for
/// every event: a draft only its organiser can open, an unlisted one reachable only by its link, a
/// members-only one for an organization's own members. A member's <c>event.created</c> rule fired
/// on somebody else's private draft and put its title in a notification.
/// </para>
/// <para>
/// The rule is the events search's for an anonymous viewer, decided where the event lives and carried
/// on the contract as <c>IsListedPublicly</c>: live, Public, neither hidden nor suspended. Such an
/// event concerns anybody watching. Anything else concerns its host alone — the organiser and, where
/// one runs it, the organization — because this module cannot check membership of a members-only
/// audience or possession of an unlisted link, and the safe reading of an audience it cannot check is
/// the one person who certainly may see it.
/// </para>
/// <para>
/// <b>Null means nobody.</b> A non-public event with no host on the payload has nobody it may
/// concern, and an empty owner set there would be read by the engine as "everybody" — the exact
/// leak this closes. Callers skip the trigger on null rather than passing <c>[]</c>.
/// </para>
/// </summary>
internal static class EventAudience
{
    public static IReadOnlyList<Guid>? For(
        bool isListedPublicly, Guid? organizerUserId, Guid? organizationId)
    {
        if (isListedPublicly)
        {
            return [];
        }

        IReadOnlyList<Guid> host =
        [
            .. new[] { organizerUserId, organizationId }
                .Where(id => id is { } value && value != Guid.Empty)
                .Select(id => id!.Value)
                .Distinct()
        ];

        return host.Count > 0 ? host : null;
    }
}
