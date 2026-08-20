using System.Collections.Frozen;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Application.Identity.Validation;
using Diaspora.Infrastructure.Identity.Options;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Infrastructure.Identity.Passwords;

/// <summary>
/// Enforces the configured password rules.
/// <para>
/// Lives behind a port rather than inside a validator so registration, change and reset
/// all get the identical answer. Messages come from the localisation catalogue, so a
/// member is told why their password was refused in their own language.
/// </para>
/// </summary>
public sealed class ConfigurablePasswordPolicy : IPasswordPolicy
{
    /// <summary>
    /// Stems of passwords that satisfy every structural rule and are still worthless.
    /// <para>
    /// Matched as substrings rather than exact values. The length floor of twelve makes
    /// exact matching almost useless — nobody submits "password", they submit
    /// "Password123456", which clears every character-class rule and is no better. Only
    /// stems of eight characters or more are listed, so the substring rule cannot start
    /// rejecting reasonable passwords that happen to contain a short common word.
    /// </para>
    /// <para>
    /// Not a substitute for a breach-corpus check. That belongs behind a service call and
    /// is a natural extension of this port; this catches the obvious cases at zero cost.
    /// </para>
    /// </summary>
    private static readonly FrozenSet<string> WeakPasswordStems = new[]
    {
        "password", "passw0rd", "p@ssword", "p@ssw0rd", "qwertyuiop", "1234567890",
        "administrator", "letmein1", "welcome1", "diaspora", "changeme", "iloveyou",
        "trustno1", "sunshine", "princess", "football", "baseball", "superman",
        "michael1", "shadow12", "monkey12", "dragon12", "master12", "abc12345"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Shortest stem in the denylist. Enforced by a test so an over-short entry cannot be
    /// added without someone noticing the false-positive risk.
    /// </summary>
    public const int MinimumWeakStemLength = 8;

    private readonly PasswordPolicyOptions _options;
    private readonly ILocalizedMessageProvider _messages;

    public ConfigurablePasswordPolicy(
        IOptions<PasswordPolicyOptions> options,
        ILocalizedMessageProvider messages)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _messages = messages;
    }

    public Result Validate(string password, PasswordPolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(password))
        {
            return Fail(IdentityMessageKeys.PasswordRequired, "Identity.Password.Required");
        }

        if (password.Length < _options.MinimumLength)
        {
            return Fail(
                IdentityMessageKeys.PasswordTooShort,
                "Identity.Password.TooShort",
                _options.MinimumLength);
        }

        if (password.Length > _options.MaximumLength)
        {
            return Fail(
                IdentityMessageKeys.PasswordTooLong,
                "Identity.Password.TooLong",
                _options.MaximumLength);
        }

        if (_options.RequireUppercase && !password.Any(char.IsUpper))
        {
            return Fail(IdentityMessageKeys.PasswordNeedsUppercase, "Identity.Password.NeedsUppercase");
        }

        if (_options.RequireLowercase && !password.Any(char.IsLower))
        {
            return Fail(IdentityMessageKeys.PasswordNeedsLowercase, "Identity.Password.NeedsLowercase");
        }

        if (_options.RequireDigit && !password.Any(char.IsDigit))
        {
            return Fail(IdentityMessageKeys.PasswordNeedsDigit, "Identity.Password.NeedsDigit");
        }

        if (_options.RequireNonAlphanumeric && password.All(char.IsLetterOrDigit))
        {
            return Fail(IdentityMessageKeys.PasswordNeedsSymbol, "Identity.Password.NeedsSymbol");
        }

        if (ContainsWeakStem(password))
        {
            return Fail(IdentityMessageKeys.PasswordCommon, "Identity.Password.TooCommon");
        }

        if (_options.ForbidPersonalInformation && ContainsPersonalInformation(password, context))
        {
            return Fail(IdentityMessageKeys.PasswordContainsPersonalInfo, "Identity.Password.PersonalInformation");
        }

        return Result.Success();
    }

    public PasswordPolicyDescriptor Describe() => new()
    {
        MinimumLength = _options.MinimumLength,
        MaximumLength = _options.MaximumLength,
        RequiresUppercase = _options.RequireUppercase,
        RequiresLowercase = _options.RequireLowercase,
        RequiresDigit = _options.RequireDigit,
        RequiresNonAlphanumeric = _options.RequireNonAlphanumeric,
        ForbidsPersonalInformation = _options.ForbidPersonalInformation,
        HistoryDepth = _options.EnforceHistory ? _options.HistoryDepth : 0
    };

    /// <summary>
    /// True when the password is built around a known-worthless stem, however it has been
    /// padded. "Password123456" and "MyPassword2026" both land here.
    /// </summary>
    private static bool ContainsWeakStem(string password) =>
        WeakPasswordStems.Any(stem => password.Contains(stem, StringComparison.OrdinalIgnoreCase));

    /// <summary>The denylist, for tests that assert its invariants.</summary>
    public static IReadOnlyCollection<string> WeakStems => WeakPasswordStems;

    /// <summary>
    /// Rejects passwords built from the member's own details. Name parts are checked from
    /// three characters up — shorter fragments produce false positives that would refuse
    /// perfectly good passwords.
    /// </summary>
    private static bool ContainsPersonalInformation(string password, PasswordPolicyContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.PhoneNumber))
        {
            var digits = new string(context.PhoneNumber.Where(char.IsDigit).ToArray());
            if (digits.Length >= 6 && password.Contains(digits[^6..], StringComparison.Ordinal))
            {
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(context.FullName))
        {
            var parts = context.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Any(part => part.Length >= 3 && password.Contains(part, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(context.Email))
        {
            var localPart = context.Email.Split('@')[0];
            if (localPart.Length >= 3 && password.Contains(localPart, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private Result Fail(string messageKey, string errorCode, params object[] arguments)
    {
        var description = arguments.Length == 0
            ? _messages.GetMessage(messageKey)
            : _messages.GetMessage(messageKey, arguments);

        return Result.Failure(Error.Validation(errorCode, description));
    }
}
