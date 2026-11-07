using System.ComponentModel.DataAnnotations;

namespace Diaspora.Infrastructure.Identity.Options;

/// <summary>
/// Identity settings that do not belong to a more specific options class.
/// </summary>
public sealed class IdentityModuleOptions
{
    public const string SectionName = "Identity";

    /// <summary>
    /// Devices a member may register. Reaching it never blocks a sign-in; the session is
    /// simply created without a device record.
    /// </summary>
    [Range(1, 100)]
    public int MaxDevicesPerUser { get; set; } = 10;

    /// <summary>
    /// Writes generated one-time codes to the application log.
    /// <para>
    /// Development only. The startup validator refuses to start outside Development with
    /// this enabled — a log containing live OTP codes is a credential store nobody is
    /// treating as one.
    /// </para>
    /// </summary>
    public bool LogOtpCodesForDevelopment { get; set; }

    /// <summary>
    /// Says when a newly registered device is one other accounts already use, as a count (D-13).
    /// Financial then holds the member's points for review, and Creator their referral.
    /// <para>
    /// Off until the privacy notice says the platform does this. Turned on, it announces devices
    /// registered from then on; it does not look back.
    /// </para>
    /// </summary>
    public bool EnableSharedDeviceSignal { get; set; }

    /// <summary>
    /// Reinstates suspended members whose suspension's end date has passed (7 Nov 2026: nothing did,
    /// so a suspension "until 21 November" lasted until somebody remembered).
    /// </summary>
    public bool EnableSuspensionExpirySweep { get; set; } = true;

    /// <summary>
    /// How often the sweep looks. A member reinstated up to this late is the cost of letting the
    /// database sleep; production sets an hour.
    /// </summary>
    [Range(1, 1440)]
    public int SuspensionExpirySweepMinutes { get; set; } = 15;
}
