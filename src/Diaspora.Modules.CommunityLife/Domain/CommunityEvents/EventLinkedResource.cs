using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Policies;

namespace Diaspora.Modules.CommunityLife.Domain.CommunityEvents;

/// <summary>
/// Something outside this module that an event points at.
/// <para>
/// <b>This one type is how B12 meets the Jobs and Marketplace requirements without referencing
/// either module.</b> A career fair lists the vacancies being recruited for; a festival names its
/// vendor booths; a series links its other sessions. Each is an identifier belonging to somebody
/// else, stored opaquely with a label the organizer supplied and resolved by the client against the
/// vertical that owns it.
/// </para>
/// <para>
/// <b>Deliberately unvalidated against the owning module, and that is a considered trade.</b>
/// Validating a job posting would mean referencing B9, which would be the first cycle in a graph
/// B2.6 exists to keep acyclic — and it would buy a check that goes stale the moment the posting
/// closes. The client resolves the reference at render time against the module that owns it, which
/// is both cheaper and more truthful. What this type does enforce is that an
/// <see cref="LinkedResourceType.ExternalLink"/> carries a URL a client can safely put in an
/// anchor.
/// </para>
/// </summary>
public sealed class EventLinkedResource
{
    private EventLinkedResource(
        LinkedResourceType resourceType, Guid resourceId, string? label, string? url, int order)
    {
        ResourceType = resourceType;
        ResourceId = resourceId;
        Label = label;
        Url = url;
        Order = order;
    }

    private EventLinkedResource() { }

    public LinkedResourceType ResourceType { get; private set; }

    /// <summary>The owning module's identifier. <see cref="Guid.Empty"/> for an external link.</summary>
    public Guid ResourceId { get; private set; }

    /// <summary>What to call it. Supplied by the organizer, because this module cannot look it up.</summary>
    public string? Label { get; private set; }

    /// <summary>Set only for an external link, and only when it is a valid http(s) URL.</summary>
    public string? Url { get; private set; }

    public int Order { get; private set; }

    public static EventLinkedResource? Create(
        LinkedResourceType resourceType,
        Guid resourceId,
        string? label = null,
        string? url = null,
        int order = 0)
    {
        var trimmedUrl = Clip(url, CommunityLifeLimits.MaxUrl);

        if (resourceType == LinkedResourceType.ExternalLink)
        {
            // An external link with nothing to link to is not a resource. Dropped rather than
            // stored, because a client rendering it would produce a dead anchor.
            if (trimmedUrl is null || !ValueObjects.ContactDetails.IsAbsoluteHttpUrl(trimmedUrl))
            {
                return null;
            }

            return new EventLinkedResource(
                resourceType,
                Guid.Empty,
                Clip(label, CommunityLifeLimits.MaxDisplayName),
                trimmedUrl,
                Math.Max(0, order));
        }

        if (resourceId == Guid.Empty)
        {
            return null;
        }

        return new EventLinkedResource(
            resourceType,
            resourceId,
            Clip(label, CommunityLifeLimits.MaxDisplayName),
            url: null,
            Math.Max(0, order));
    }

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }
}
