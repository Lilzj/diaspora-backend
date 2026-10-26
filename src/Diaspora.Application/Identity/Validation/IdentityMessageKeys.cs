namespace Diaspora.Application.Identity.Validation;

/// <summary>
/// Keys for every member-facing validation message in the identity module.
/// <para>
/// Validators emit keys, never literals. Diaspora's members span many languages, and a
/// hardcoded English string in a validator is a translation that can never happen.
/// </para>
/// </summary>
public static class IdentityMessageKeys
{
    public const string FullNameRequired = "identity.validation.fullName.required";
    public const string FullNameLength = "identity.validation.fullName.length";

    public const string PhoneRequired = "identity.validation.phone.required";
    public const string PhoneFormat = "identity.validation.phone.format";

    public const string PasswordRequired = "identity.validation.password.required";
    public const string PasswordTooShort = "identity.validation.password.tooShort";
    public const string PasswordTooLong = "identity.validation.password.tooLong";
    public const string PasswordNeedsUppercase = "identity.validation.password.needsUppercase";
    public const string PasswordNeedsLowercase = "identity.validation.password.needsLowercase";
    public const string PasswordNeedsDigit = "identity.validation.password.needsDigit";
    public const string PasswordNeedsSymbol = "identity.validation.password.needsSymbol";
    public const string PasswordContainsPersonalInfo = "identity.validation.password.personalInfo";
    public const string PasswordCommon = "identity.validation.password.common";

    public const string NationalityRequired = "identity.validation.nationality.required";
    public const string NationalityFormat = "identity.validation.nationality.format";

    public const string CountryOfResidenceRequired = "identity.validation.countryOfResidence.required";
    public const string CountryOfResidenceFormat = "identity.validation.countryOfResidence.format";

    public const string LanguageFormat = "identity.validation.language.format";
    public const string TimeZoneFormat = "identity.validation.timeZone.format";
    public const string ReferralCodeFormat = "identity.validation.referralCode.format";
    public const string EmailFormat = "identity.validation.email.format";

    /// <summary>Sign-in and recovery both identify the member by email address.</summary>
    public const string EmailRequired = "identity.validation.email.required";

    public const string OtpRequired = "identity.validation.otp.required";
    public const string OtpFormat = "identity.validation.otp.format";
    public const string OtpPurposeUnknown = "identity.validation.otp.purposeUnknown";

    /// <summary>
    /// The body of the SMS that carries a one-time code.
    /// <para>
    /// In the catalogue rather than in the SMS adapter because it is the one string in that path
    /// a member actually reads, and the adapter is a vendor wire that should not own copy. Takes
    /// the code and the minutes it lasts, in that order.
    /// </para>
    /// </summary>
    public const string OtpSmsBody = "identity.otp.smsBody";

    /// <summary>Subject line of the email carrying a one-time code.</summary>
    /// <remarks>
    /// The code is deliberately <b>not</b> in the subject. A subject is what a phone shows on a
    /// locked screen, and a recovery code readable without unlocking the device is a weaker code
    /// than the one the body carries.
    /// </remarks>
    public const string OtpEmailSubject = "identity.otp.emailSubject";

    public const string OtpEmailBody = "identity.otp.emailBody";

    /// <summary>The small print under a recovery email.</summary>
    /// <remarks>
    /// Separate from the body so the layout can set it below the rule, where small print belongs.
    /// It carries the one instruction that matters to somebody who did not ask for the code.
    /// </remarks>
    public const string OtpEmailFooter = "identity.otp.emailFooter";

    /// <summary>Subject of the email that proves a member receives mail at their address.</summary>
    public const string EmailVerificationSubject = "identity.emailVerification.subject";

    /// <summary>Body; <c>{0}</c> is how many hours the link stays valid.</summary>
    public const string EmailVerificationBody = "identity.emailVerification.body";

    /// <summary>The label on the link.</summary>
    public const string EmailVerificationButton = "identity.emailVerification.button";

    /// <summary>Why the member received it, and what to do if they did not ask.</summary>
    public const string EmailVerificationFooter = "identity.emailVerification.footer";

    /// <summary>Subject of the link sent to the NEW address when a member asks to move their sign-in address.</summary>
    public const string EmailChangeSubject = "identity.emailChange.subject";

    /// <summary>Body; <c>{0}</c> is how many hours the link stays valid.</summary>
    public const string EmailChangeBody = "identity.emailChange.body";

    /// <summary>The label on the link.</summary>
    public const string EmailChangeButton = "identity.emailChange.button";

    /// <summary>Why the new address received it, and what to do if nobody there asked.</summary>
    public const string EmailChangeFooter = "identity.emailChange.footer";

    /// <summary>Sent to the new address instead of a link when another account already signs in with it.</summary>
    public const string EmailChangeUnavailableSubject = "identity.emailChange.unavailableSubject";

    /// <summary>Body of the address-unavailable email.</summary>
    public const string EmailChangeUnavailableBody = "identity.emailChange.unavailableBody";

    /// <summary>Sent to the OLD address when a change is requested.</summary>
    public const string EmailChangeRequestedSubject = "identity.emailChange.requestedSubject";

    /// <summary>Body; <c>{0}</c> is the masked new address, <c>{1}</c> the hours the link stays valid.</summary>
    public const string EmailChangeRequestedBody = "identity.emailChange.requestedBody";

    /// <summary>Sent to the OLD address when a change completes.</summary>
    public const string EmailChangeCompletedSubject = "identity.emailChange.completedSubject";

    /// <summary>Body; <c>{0}</c> is the masked new address.</summary>
    public const string EmailChangeCompletedBody = "identity.emailChange.completedBody";

    public const string RefreshTokenRequired = "identity.validation.refreshToken.required";
    public const string CurrentPasswordRequired = "identity.validation.currentPassword.required";

    public const string DeviceIdentifierRequired = "identity.validation.device.identifierRequired";
    public const string DeviceIdentifierLength = "identity.validation.device.identifierLength";
    public const string PlatformUnknown = "identity.validation.device.platformUnknown";
    public const string PushTokenLength = "identity.validation.device.pushTokenLength";

    public const string ReasonRequired = "identity.validation.reason.required";
    public const string ReasonLength = "identity.validation.reason.length";
    public const string TrustOutcomeUnknown = "identity.validation.trust.outcomeUnknown";
    public const string UserIdRequired = "identity.validation.userId.required";
    public const string NotesLength = "identity.validation.notes.length";
}
