using System.Security.Cryptography;
using System.Text;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.Infrastructure.Identity.Options;
using Microsoft.Extensions.Options;

namespace Diaspora.Infrastructure.Identity.Otp;

/// <summary>
/// Generates numeric one-time codes from a cryptographic RNG.
/// <para>
/// A six-digit code has only a million possibilities, so the generator has to be
/// unpredictable — <c>Random</c> would let an attacker who saw one code narrow the next.
/// Brute force is prevented separately, by the attempt budget on the challenge.
/// </para>
/// <para>
/// Codes are hashed with a keyed HMAC rather than a bare digest. The search space is small
/// enough to enumerate instantly, so an unkeyed hash of a six-digit code offers no
/// protection at all if the table leaks; the key does.
/// </para>
/// </summary>
public sealed class SecureOtpCodeGenerator : IOtpCodeGenerator
{
    private readonly OtpOptions _options;
    private readonly byte[] _hashKey;

    public SecureOtpCodeGenerator(IOptions<OtpOptions> options, IOptions<JwtOptions> jwtOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(jwtOptions);

        _options = options.Value;

        // Derived from the signing key so it inherits the same secret-store handling and
        // rotation story, rather than adding a second secret nobody remembers to rotate.
        _hashKey = SHA256.HashData(
            Encoding.UTF8.GetBytes($"otp-hmac:{jwtOptions.Value.SigningKey}"));
    }

    public string Generate()
    {
        var digits = new char[_options.CodeLength];

        for (var i = 0; i < digits.Length; i++)
        {
            digits[i] = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));
        }

        return new string(digits);
    }

    public SecretHash ComputeHash(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var digest = HMACSHA256.HashData(_hashKey, Encoding.UTF8.GetBytes(code.Trim()));
        return SecretHash.Create(Convert.ToHexString(digest));
    }
}
