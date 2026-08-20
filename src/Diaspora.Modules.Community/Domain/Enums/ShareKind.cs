namespace Diaspora.Modules.Community.Domain.Enums;

/// <summary>
/// How a member passed content along.
/// <para>
/// The four are genuinely different events, and collapsing them into one share counter is what
/// makes most social analytics useless. An <see cref="External"/> share leaves the platform and
/// can never be attributed further; a <see cref="Reshare"/> creates a new feed entry that
/// carries its own engagement; a <see cref="Quote"/> creates a new post that happens to embed
/// this one. Only <see cref="Internal"/> is a simple pass-along.
/// </para>
/// </summary>
public enum ShareKind
{
    /// <summary>Sent to another member or a conversation inside the platform.</summary>
    Internal = 0,

    /// <summary>Handed to the operating system share sheet. Destination recorded if known.</summary>
    External = 1,

    /// <summary>Reposted with commentary. Produces a new post referencing this one.</summary>
    Quote = 2,

    /// <summary>Reposted without commentary. Produces a feed entry, not a new post.</summary>
    Reshare = 3
}
