using System.Collections.Frozen;

namespace Diaspora.Application.Identity.Validation;

/// <summary>
/// Translations for every key in <see cref="IdentityMessageKeys"/>.
/// <para>
/// Held in code rather than in resx files: the catalogue is small, ships with the module
/// it belongs to, and stays diff-reviewable alongside the validators that use it. Adding
/// a language is a dictionary entry. If translation ever moves to a service, only the
/// provider behind <c>ILocalizedMessageProvider</c> changes.
/// </para>
/// <para>
/// English, French, Portuguese and Arabic cover the largest share of Diaspora's early
/// corridors. Any key missing from a language falls back to English.
/// </para>
/// </summary>
public static class IdentityMessageCatalog
{
    public const string DefaultLanguage = "en";

    private static readonly FrozenDictionary<string, FrozenDictionary<string, string>> Translations =
        BuildTranslations();

    public static IReadOnlyCollection<string> SupportedLanguages => Translations.Keys;

    /// <summary>
    /// Resolves a key for a language tag, narrowing "pt-BR" to "pt" before falling back
    /// to English, then to the key itself so a gap is visible rather than silent.
    /// </summary>
    public static string Resolve(string key, string? language)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;

        var normalized = NormalizeLanguage(language);

        if (Translations.TryGetValue(normalized, out var exact) && exact.TryGetValue(key, out var text))
        {
            return text;
        }

        var primary = PrimarySubtag(normalized);
        if (primary != normalized &&
            Translations.TryGetValue(primary, out var basic) &&
            basic.TryGetValue(key, out var basicText))
        {
            return basicText;
        }

        return Translations[DefaultLanguage].TryGetValue(key, out var fallback) ? fallback : key;
    }

    private static string NormalizeLanguage(string? language) =>
        string.IsNullOrWhiteSpace(language)
            ? DefaultLanguage
            : language.Trim().Replace('_', '-').ToLowerInvariant();

    private static string PrimarySubtag(string language)
    {
        var separator = language.IndexOf('-', StringComparison.Ordinal);
        return separator < 0 ? language : language[..separator];
    }

    private static FrozenDictionary<string, FrozenDictionary<string, string>> BuildTranslations()
    {
        var english = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [IdentityMessageKeys.FullNameRequired] = "Your full name is required.",
            [IdentityMessageKeys.FullNameLength] = "Your full name must be between 2 and 120 characters.",
            [IdentityMessageKeys.PhoneRequired] = "A phone number is required.",
            [IdentityMessageKeys.EmailRequired] = "An email address is required.",
            [IdentityMessageKeys.PhoneFormat] = "Enter a valid international phone number, for example +2348012345678.",
            [IdentityMessageKeys.PasswordRequired] = "A password is required.",
            [IdentityMessageKeys.PasswordTooShort] = "Your password must be at least {0} characters long.",
            [IdentityMessageKeys.PasswordTooLong] = "Your password must be no more than {0} characters long.",
            [IdentityMessageKeys.PasswordNeedsUppercase] = "Your password must contain an uppercase letter.",
            [IdentityMessageKeys.PasswordNeedsLowercase] = "Your password must contain a lowercase letter.",
            [IdentityMessageKeys.PasswordNeedsDigit] = "Your password must contain a digit.",
            [IdentityMessageKeys.PasswordNeedsSymbol] = "Your password must contain a symbol.",
            [IdentityMessageKeys.PasswordContainsPersonalInfo] = "Your password must not contain your name or phone number.",
            [IdentityMessageKeys.PasswordCommon] = "This password is too common. Choose something harder to guess.",
            [IdentityMessageKeys.NationalityRequired] = "Your nationality is required.",
            [IdentityMessageKeys.NationalityFormat] = "Nationality must be a two-letter country code, for example NG.",
            [IdentityMessageKeys.CountryOfResidenceRequired] = "Your country of residence is required.",
            [IdentityMessageKeys.CountryOfResidenceFormat] = "Country of residence must be a two-letter country code, for example CA.",
            [IdentityMessageKeys.LanguageFormat] = "Enter a valid language tag, for example en or pt-BR.",
            [IdentityMessageKeys.TimeZoneFormat] = "Enter a valid time zone, for example Africa/Lagos.",
            [IdentityMessageKeys.EmailFormat] = "Enter a valid email address.",
            [IdentityMessageKeys.OtpRequired] = "A verification code is required.",
            [IdentityMessageKeys.OtpFormat] = "The verification code must be {0} digits.",
            [IdentityMessageKeys.OtpPurposeUnknown] = "That verification purpose is not recognised.",
            [IdentityMessageKeys.OtpSmsBody] =
                "Your Diaspora code is {0}. It expires in {1} minutes. Never share it with anyone.",
            [IdentityMessageKeys.OtpEmailSubject] = "Your Diaspora verification code",
            [IdentityMessageKeys.OtpEmailBody] =
                "Your Diaspora code is {0}. It expires in {1} minutes. Never share it "
                + "with anyone.\n\nIf you did not request this code, you can ignore "
                + "this message. Your password has not been changed.",
            [IdentityMessageKeys.OtpEmailFooter] =
                "If you did not ask to reset your password, no action is needed and your password has not changed.",
            [IdentityMessageKeys.RefreshTokenRequired] = "A refresh token is required.",
            [IdentityMessageKeys.CurrentPasswordRequired] = "Your current password is required.",
            [IdentityMessageKeys.DeviceIdentifierRequired] = "A device identifier is required.",
            [IdentityMessageKeys.DeviceIdentifierLength] = "The device identifier is too long.",
            [IdentityMessageKeys.PlatformUnknown] = "That platform is not recognised.",
            [IdentityMessageKeys.PushTokenLength] = "The push token is too long.",
            [IdentityMessageKeys.ReasonRequired] = "A reason is required.",
            [IdentityMessageKeys.ReasonLength] = "The reason is too long.",
            [IdentityMessageKeys.TrustOutcomeUnknown] = "That trust decision outcome is not recognised.",
            [IdentityMessageKeys.UserIdRequired] = "A user identifier is required.",
            [IdentityMessageKeys.NotesLength] = "The notes are too long."
        };

        var french = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [IdentityMessageKeys.FullNameRequired] = "Votre nom complet est requis.",
            [IdentityMessageKeys.FullNameLength] = "Votre nom complet doit contenir entre 2 et 120 caractères.",
            [IdentityMessageKeys.PhoneRequired] = "Un numéro de téléphone est requis.",
            [IdentityMessageKeys.EmailRequired] = "Une adresse e-mail est requise.",
            [IdentityMessageKeys.PhoneFormat] = "Saisissez un numéro international valide, par exemple +2348012345678.",
            [IdentityMessageKeys.PasswordRequired] = "Un mot de passe est requis.",
            [IdentityMessageKeys.PasswordTooShort] = "Votre mot de passe doit contenir au moins {0} caractères.",
            [IdentityMessageKeys.PasswordTooLong] = "Votre mot de passe ne doit pas dépasser {0} caractères.",
            [IdentityMessageKeys.PasswordNeedsUppercase] = "Votre mot de passe doit contenir une majuscule.",
            [IdentityMessageKeys.PasswordNeedsLowercase] = "Votre mot de passe doit contenir une minuscule.",
            [IdentityMessageKeys.PasswordNeedsDigit] = "Votre mot de passe doit contenir un chiffre.",
            [IdentityMessageKeys.PasswordNeedsSymbol] = "Votre mot de passe doit contenir un symbole.",
            [IdentityMessageKeys.PasswordContainsPersonalInfo] = "Votre mot de passe ne doit pas contenir votre nom ni votre numéro de téléphone.",
            [IdentityMessageKeys.PasswordCommon] = "Ce mot de passe est trop courant. Choisissez-en un plus difficile à deviner.",
            [IdentityMessageKeys.NationalityRequired] = "Votre nationalité est requise.",
            [IdentityMessageKeys.NationalityFormat] = "La nationalité doit être un code pays à deux lettres, par exemple NG.",
            [IdentityMessageKeys.CountryOfResidenceRequired] = "Votre pays de résidence est requis.",
            [IdentityMessageKeys.CountryOfResidenceFormat] = "Le pays de résidence doit être un code pays à deux lettres, par exemple CA.",
            [IdentityMessageKeys.LanguageFormat] = "Saisissez une étiquette de langue valide, par exemple fr ou pt-BR.",
            [IdentityMessageKeys.TimeZoneFormat] = "Saisissez un fuseau horaire valide, par exemple Africa/Lagos.",
            [IdentityMessageKeys.EmailFormat] = "Saisissez une adresse e-mail valide.",
            [IdentityMessageKeys.OtpRequired] = "Un code de vérification est requis.",
            [IdentityMessageKeys.OtpFormat] = "Le code de vérification doit comporter {0} chiffres.",
            [IdentityMessageKeys.OtpPurposeUnknown] = "Ce type de vérification n'est pas reconnu.",
            [IdentityMessageKeys.OtpSmsBody] =
                "Votre code Diaspora est {0}. Il expire dans {1} minutes. Ne le partagez avec personne.",
            [IdentityMessageKeys.OtpEmailSubject] = "Votre code de vérification Diaspora",
            [IdentityMessageKeys.OtpEmailBody] =
                "Votre code Diaspora est {0}. Il expire dans {1} minutes. Ne le partagez "
                + "avec personne.\n\nSi vous n'avez pas demandé ce code, vous "
                + "pouvez ignorer ce message. Votre mot de passe n'a pas été modifié.",
            [IdentityMessageKeys.OtpEmailFooter] =
                "Si vous n'avez pas demandé à réinitialiser votre mot de passe, aucune action "
                + "n'est nécessaire et votre mot de passe reste inchangé.",
            [IdentityMessageKeys.RefreshTokenRequired] = "Un jeton de rafraîchissement est requis.",
            [IdentityMessageKeys.CurrentPasswordRequired] = "Votre mot de passe actuel est requis.",
            [IdentityMessageKeys.DeviceIdentifierRequired] = "Un identifiant d'appareil est requis.",
            [IdentityMessageKeys.DeviceIdentifierLength] = "L'identifiant d'appareil est trop long.",
            [IdentityMessageKeys.PlatformUnknown] = "Cette plateforme n'est pas reconnue.",
            [IdentityMessageKeys.PushTokenLength] = "Le jeton de notification est trop long.",
            [IdentityMessageKeys.ReasonRequired] = "Un motif est requis.",
            [IdentityMessageKeys.ReasonLength] = "Le motif est trop long.",
            [IdentityMessageKeys.TrustOutcomeUnknown] = "Ce résultat de décision de confiance n'est pas reconnu.",
            [IdentityMessageKeys.UserIdRequired] = "Un identifiant d'utilisateur est requis.",
            [IdentityMessageKeys.NotesLength] = "Les notes sont trop longues."
        };

        var portuguese = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [IdentityMessageKeys.FullNameRequired] = "O seu nome completo é obrigatório.",
            [IdentityMessageKeys.FullNameLength] = "O seu nome completo deve ter entre 2 e 120 caracteres.",
            [IdentityMessageKeys.PhoneRequired] = "É necessário um número de telefone.",
            [IdentityMessageKeys.EmailRequired] = "É necessário um endereço de e-mail.",
            [IdentityMessageKeys.PhoneFormat] = "Introduza um número internacional válido, por exemplo +2348012345678.",
            [IdentityMessageKeys.PasswordRequired] = "É necessária uma palavra-passe.",
            [IdentityMessageKeys.PasswordTooShort] = "A sua palavra-passe deve ter pelo menos {0} caracteres.",
            [IdentityMessageKeys.PasswordTooLong] = "A sua palavra-passe não deve exceder {0} caracteres.",
            [IdentityMessageKeys.PasswordNeedsUppercase] = "A sua palavra-passe deve conter uma letra maiúscula.",
            [IdentityMessageKeys.PasswordNeedsLowercase] = "A sua palavra-passe deve conter uma letra minúscula.",
            [IdentityMessageKeys.PasswordNeedsDigit] = "A sua palavra-passe deve conter um dígito.",
            [IdentityMessageKeys.PasswordNeedsSymbol] = "A sua palavra-passe deve conter um símbolo.",
            [IdentityMessageKeys.PasswordContainsPersonalInfo] = "A sua palavra-passe não deve conter o seu nome nem o seu número de telefone.",
            [IdentityMessageKeys.PasswordCommon] = "Esta palavra-passe é demasiado comum. Escolha uma mais difícil de adivinhar.",
            [IdentityMessageKeys.NationalityRequired] = "A sua nacionalidade é obrigatória.",
            [IdentityMessageKeys.NationalityFormat] = "A nacionalidade deve ser um código de país de duas letras, por exemplo NG.",
            [IdentityMessageKeys.CountryOfResidenceRequired] = "O seu país de residência é obrigatório.",
            [IdentityMessageKeys.CountryOfResidenceFormat] = "O país de residência deve ser um código de país de duas letras, por exemplo CA.",
            [IdentityMessageKeys.LanguageFormat] = "Introduza uma etiqueta de idioma válida, por exemplo pt ou pt-BR.",
            [IdentityMessageKeys.TimeZoneFormat] = "Introduza um fuso horário válido, por exemplo Africa/Lagos.",
            [IdentityMessageKeys.EmailFormat] = "Introduza um endereço de e-mail válido.",
            [IdentityMessageKeys.OtpRequired] = "É necessário um código de verificação.",
            [IdentityMessageKeys.OtpFormat] = "O código de verificação deve ter {0} dígitos.",
            [IdentityMessageKeys.OtpPurposeUnknown] = "Esse tipo de verificação não é reconhecido.",
            [IdentityMessageKeys.OtpSmsBody] =
                "O seu código Diaspora é {0}. Expira em {1} minutos. Nunca o partilhe com ninguém.",
            [IdentityMessageKeys.OtpEmailSubject] = "O seu código de verificação Diaspora",
            [IdentityMessageKeys.OtpEmailBody] =
                "O seu código Diaspora é {0}. Expira em {1} minutos. Nunca o partilhe "
                + "com ninguém.\n\nSe não solicitou este código, pode ignorar esta "
                + "mensagem. A sua palavra-passe não foi alterada.",
            [IdentityMessageKeys.OtpEmailFooter] =
                "Se não pediu para redefinir a sua palavra-passe, não é necessária qualquer "
                + "ação e a sua palavra-passe permanece inalterada.",
            [IdentityMessageKeys.RefreshTokenRequired] = "É necessário um token de atualização.",
            [IdentityMessageKeys.CurrentPasswordRequired] = "A sua palavra-passe atual é obrigatória.",
            [IdentityMessageKeys.DeviceIdentifierRequired] = "É necessário um identificador de dispositivo.",
            [IdentityMessageKeys.DeviceIdentifierLength] = "O identificador do dispositivo é demasiado longo.",
            [IdentityMessageKeys.PlatformUnknown] = "Essa plataforma não é reconhecida.",
            [IdentityMessageKeys.PushTokenLength] = "O token de notificação é demasiado longo.",
            [IdentityMessageKeys.ReasonRequired] = "É necessário um motivo.",
            [IdentityMessageKeys.ReasonLength] = "O motivo é demasiado longo.",
            [IdentityMessageKeys.TrustOutcomeUnknown] = "Esse resultado de decisão de confiança não é reconhecido.",
            [IdentityMessageKeys.UserIdRequired] = "É necessário um identificador de utilizador.",
            [IdentityMessageKeys.NotesLength] = "As notas são demasiado longas."
        };

        var arabic = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [IdentityMessageKeys.FullNameRequired] = "الاسم الكامل مطلوب.",
            [IdentityMessageKeys.FullNameLength] = "يجب أن يتراوح الاسم الكامل بين 2 و120 حرفًا.",
            [IdentityMessageKeys.PhoneRequired] = "رقم الهاتف مطلوب.",
            [IdentityMessageKeys.EmailRequired] = "عنوان البريد الإلكتروني مطلوب.",
            [IdentityMessageKeys.PhoneFormat] = "أدخل رقم هاتف دولي صحيح، مثل ‎+2348012345678.",
            [IdentityMessageKeys.PasswordRequired] = "كلمة المرور مطلوبة.",
            [IdentityMessageKeys.PasswordTooShort] = "يجب ألا تقل كلمة المرور عن {0} أحرف.",
            [IdentityMessageKeys.PasswordTooLong] = "يجب ألا تزيد كلمة المرور عن {0} حرفًا.",
            [IdentityMessageKeys.PasswordNeedsUppercase] = "يجب أن تحتوي كلمة المرور على حرف كبير.",
            [IdentityMessageKeys.PasswordNeedsLowercase] = "يجب أن تحتوي كلمة المرور على حرف صغير.",
            [IdentityMessageKeys.PasswordNeedsDigit] = "يجب أن تحتوي كلمة المرور على رقم.",
            [IdentityMessageKeys.PasswordNeedsSymbol] = "يجب أن تحتوي كلمة المرور على رمز.",
            [IdentityMessageKeys.PasswordContainsPersonalInfo] = "يجب ألا تحتوي كلمة المرور على اسمك أو رقم هاتفك.",
            [IdentityMessageKeys.PasswordCommon] = "كلمة المرور هذه شائعة جدًا. اختر كلمة يصعب تخمينها.",
            [IdentityMessageKeys.NationalityRequired] = "الجنسية مطلوبة.",
            [IdentityMessageKeys.NationalityFormat] = "يجب أن تكون الجنسية رمز دولة من حرفين، مثل NG.",
            [IdentityMessageKeys.CountryOfResidenceRequired] = "بلد الإقامة مطلوب.",
            [IdentityMessageKeys.CountryOfResidenceFormat] = "يجب أن يكون بلد الإقامة رمز دولة من حرفين، مثل CA.",
            [IdentityMessageKeys.LanguageFormat] = "أدخل رمز لغة صحيحًا، مثل ar أو pt-BR.",
            [IdentityMessageKeys.TimeZoneFormat] = "أدخل منطقة زمنية صحيحة، مثل Africa/Lagos.",
            [IdentityMessageKeys.EmailFormat] = "أدخل عنوان بريد إلكتروني صحيحًا.",
            [IdentityMessageKeys.OtpRequired] = "رمز التحقق مطلوب.",
            [IdentityMessageKeys.OtpFormat] = "يجب أن يتكون رمز التحقق من {0} أرقام.",
            [IdentityMessageKeys.OtpPurposeUnknown] = "غرض التحقق هذا غير معروف.",
            [IdentityMessageKeys.OtpSmsBody] =
                "رمز Diaspora الخاص بك هو {0}. تنتهي صلاحيته خلال {1} دقيقة. لا تشاركه مع أحد.",
            [IdentityMessageKeys.OtpEmailSubject] = "رمز التحقق الخاص بك في Diaspora",
            [IdentityMessageKeys.OtpEmailBody] =
                "رمز Diaspora الخاص بك هو {0}. تنتهي صلاحيته خلال {1} دقيقة. لا تشاركه مع أحد.\n\n"
                + "إذا لم تطلب هذا الرمز، يمكنك تجاهل هذه الرسالة. لم يتم تغيير كلمة المرور الخاصة بك.",
            [IdentityMessageKeys.OtpEmailFooter] =
                "إذا لم تطلب إعادة تعيين كلمة المرور، فلا حاجة لأي إجراء ولم تتغير كلمة المرور الخاصة بك.",
            [IdentityMessageKeys.RefreshTokenRequired] = "رمز التحديث مطلوب.",
            [IdentityMessageKeys.CurrentPasswordRequired] = "كلمة المرور الحالية مطلوبة.",
            [IdentityMessageKeys.DeviceIdentifierRequired] = "معرّف الجهاز مطلوب.",
            [IdentityMessageKeys.DeviceIdentifierLength] = "معرّف الجهاز طويل جدًا.",
            [IdentityMessageKeys.PlatformUnknown] = "هذه المنصة غير معروفة.",
            [IdentityMessageKeys.PushTokenLength] = "رمز الإشعارات طويل جدًا.",
            [IdentityMessageKeys.ReasonRequired] = "السبب مطلوب.",
            [IdentityMessageKeys.ReasonLength] = "السبب طويل جدًا.",
            [IdentityMessageKeys.TrustOutcomeUnknown] = "نتيجة قرار الثقة هذه غير معروفة.",
            [IdentityMessageKeys.UserIdRequired] = "معرّف المستخدم مطلوب.",
            [IdentityMessageKeys.NotesLength] = "الملاحظات طويلة جدًا."
        };

        return new Dictionary<string, FrozenDictionary<string, string>>(StringComparer.Ordinal)
        {
            ["en"] = english.ToFrozenDictionary(StringComparer.Ordinal),
            ["fr"] = french.ToFrozenDictionary(StringComparer.Ordinal),
            ["pt"] = portuguese.ToFrozenDictionary(StringComparer.Ordinal),
            ["ar"] = arabic.ToFrozenDictionary(StringComparer.Ordinal)
        }.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
