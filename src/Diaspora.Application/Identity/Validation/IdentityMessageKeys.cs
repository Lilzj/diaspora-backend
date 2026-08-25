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
    public const string EmailFormat = "identity.validation.email.format";

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
