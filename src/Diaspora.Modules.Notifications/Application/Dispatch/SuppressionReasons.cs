namespace Diaspora.Modules.Notifications.Application.Dispatch;

/// <summary>
/// Why a channel was not used, as stable machine codes.
/// <para>
/// Recorded on the delivery row and read by an operator answering "why did they not get it". The
/// distinctions are the ones that change the answer a member deserves: <see cref="ChannelDisabled"/>
/// and <see cref="CategoryMuted"/> are both the member's own choice and are fixed on different
/// screens; <see cref="DoNotDisturb"/> is temporary; <see cref="Throttled"/> is the platform
/// protecting them from a thread they are subscribed to; <see cref="NoEndpoint"/> is nobody's fault.
/// </para>
/// </summary>
public static class SuppressionReasons
{
    public const string ChannelDisabled = "ChannelDisabled";
    public const string CategoryMuted = "CategoryMuted";
    public const string DoNotDisturb = "DoNotDisturb";
    public const string Throttled = "Throttled";
    public const string RulePolicy = "RulePolicy";
    public const string SilentPriority = "SilentPriority";
    public const string NoEndpoint = "NoEndpoint";
    public const string ChannelNotConfigured = "ChannelNotConfigured";
}
