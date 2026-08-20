namespace Diaspora.Modules.Notifications.Contracts.V1.Requests;

/// <summary>One category's settings, per channel.</summary>
public sealed record CategoryChannelPreferenceRequest
{
    public required string CategoryKey { get; init; }

    public bool? InApp { get; init; }

    public bool? Push { get; init; }

    public bool? Email { get; init; }

    public bool? UseDigest { get; init; }

    /// <summary>Silences the category entirely. Refused for a mandatory category.</summary>
    public bool? IsMuted { get; init; }
}
