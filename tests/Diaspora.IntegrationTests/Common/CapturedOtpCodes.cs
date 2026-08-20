using System.Collections.Concurrent;
using Diaspora.Domain.Identity.Verification;

namespace Diaspora.IntegrationTests.Common;

/// <summary>
/// Captures the one-time codes the tests need to redeem.
/// <para>
/// The alternative — parsing them out of the log — would couple the suite to a log format
/// and require the "write live codes to the log" development flag to be on. Substituting
/// the delivery port is the same thing a real SMS gateway substitution does.
/// </para>
/// </summary>
public sealed class CapturedOtpCodes
{
    private readonly ConcurrentDictionary<string, string> _codes = new(StringComparer.Ordinal);

    public void Record(string phoneNumber, OtpPurpose purpose, string code) =>
        _codes[Key(phoneNumber, purpose)] = code;

    public string LatestFor(string phoneNumber, OtpPurpose purpose = OtpPurpose.PhoneVerification) =>
        _codes.TryGetValue(Key(phoneNumber, purpose), out var code)
            ? code
            : throw new InvalidOperationException(
                $"No {purpose} code was delivered to {phoneNumber}.");

    public bool HasCodeFor(string phoneNumber, OtpPurpose purpose) =>
        _codes.ContainsKey(Key(phoneNumber, purpose));

    public void Clear() => _codes.Clear();

    private static string Key(string phoneNumber, OtpPurpose purpose) => $"{purpose}:{phoneNumber}";
}
