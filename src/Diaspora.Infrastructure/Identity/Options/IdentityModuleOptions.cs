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
}
