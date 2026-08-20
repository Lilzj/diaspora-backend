using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.CommunityEvents;

/// <summary>
/// Somebody billed to speak at an event.
/// <para>
/// <b>Not a membership and not a user.</b> The keynote at a career fair is usually somebody with no
/// account on this platform, and requiring one would leave organizers unable to bill the person
/// members are coming to hear. When a speaker does have an account, <see cref="MemberUserId"/>
/// links the two.
/// </para>
/// </summary>
public sealed class EventSpeaker : Entity<Guid>
{
    private EventSpeaker(
        Guid id,
        string name,
        string? title,
        string? organization,
        string? biography,
        Guid? photoMediaId,
        Guid? memberUserId,
        int order) : base(id)
    {
        Name = name;
        Title = title;
        Organization = organization;
        Biography = biography;
        PhotoMediaId = photoMediaId;
        MemberUserId = memberUserId;
        Order = order;
    }

    private EventSpeaker() { }

    public string Name { get; private set; } = string.Empty;

    /// <summary>What they do — "Immigration Solicitor", "Head of Talent".</summary>
    public string? Title { get; private set; }

    /// <summary>Who they are from. Free text, and deliberately not a provider reference.</summary>
    public string? Organization { get; private set; }

    public string? Biography { get; private set; }

    /// <summary>A B5 asset identifier. Never a URL.</summary>
    public Guid? PhotoMediaId { get; private set; }

    public Guid? MemberUserId { get; private set; }

    public int Order { get; private set; }

    public static EventSpeaker Create(
        Guid id,
        string? name,
        string? title = null,
        string? organization = null,
        string? biography = null,
        Guid? photoMediaId = null,
        Guid? memberUserId = null,
        int order = 0) =>
        new(
            id,
            Clip(name, CommunityLifeLimits.MaxDisplayName) ?? string.Empty,
            Clip(title, CommunityLifeLimits.MaxDisplayName),
            Clip(organization, CommunityLifeLimits.MaxDisplayName),
            Clip(biography, CommunityLifeLimits.MaxNoteLength),
            photoMediaId == Guid.Empty ? null : photoMediaId,
            memberUserId == Guid.Empty ? null : memberUserId,
            Math.Max(0, order));

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
