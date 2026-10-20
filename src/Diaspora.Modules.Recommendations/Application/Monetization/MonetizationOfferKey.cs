using System.Security.Cryptography;
using System.Text;

namespace Diaspora.Modules.Recommendations.Application.Monetization;

/// <summary>
/// The stable identifier of a monetization suggestion.
/// <para>
/// <b>Derived, not stored, and that is what lets a dismissal stick.</b> The feedback routes key a
/// dismissal on a target identifier, and the same suggestion must produce the same identifier on
/// every request or a dismissed card would come back under a new one.
/// </para>
/// <para>
/// <b>A promotion suggestion is keyed on the item, not the product.</b> "Promote this listing" is
/// the suggestion; which product is named can change (a product bought before is preferred), and a
/// member who dismissed it meant the listing, not one product — keying on the product would offer
/// the next product for the same listing straight back. Keying on the item also keeps "promote this
/// listing" and "promote that listing" apart. A plan suggestion is keyed on the plan code.
/// </para>
/// <para>
/// RFC 9562 version 8 over SHA-256 rather than version 5 over SHA-1: the bytes only need to be
/// stable and well spread, and SHA-1 is refused by the analyzers this build runs.
/// </para>
/// </summary>
public static class MonetizationOfferKey
{
    public static Guid ForSubject(string subjectType, Guid subjectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectType);

        return Create(string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"promote:{subjectType.Trim().ToLowerInvariant()}:{subjectId:N}"));
    }

    public static Guid ForPlan(string planCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planCode);

        return Create("plan:" + planCode.Trim().ToLowerInvariant());
    }

    private static Guid Create(string name)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes("diaspora.recommendations.monetization/" + name), hash);

        var bytes = hash[..16];

        // Version 8 (custom) and the RFC variant, so the value reads as a well-formed UUID.
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes, bigEndian: true);
    }
}
